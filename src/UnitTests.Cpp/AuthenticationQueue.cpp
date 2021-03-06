#include "pch.h"

//Provides a way to store end easily access client instances.
#include "stormancer/IClientFactory.h"

//Provides APIs related to authentication & user management.
#include "Users/Users.hpp"
#include "Limits/connectionQueue.hpp"

//Declares MainThreadActionDispatcher, a class that enables the dev to run stormancer callbacks & continuations on the thread of their choice.
#include "stormancer/IActionDispatcher.h"

constexpr  char* ServerEndpoint = "http://localhost";//"http://gc3.stormancer.com";
constexpr  char* Account = "tests";
constexpr  char* Application = "queue-test";

enum AgentState
{
	InQueue,
	Connected,
	Disconnecting,
	Disconnected
};
pplx::task<bool> runAgent(int id, AgentState& state)
{

	auto client = Stormancer::IClientFactory::GetClient(id);

	auto users = client->dependencyResolver().resolve<Stormancer::Users::UsersApi>();

	//Configure authentication to use the ephemeral (anonymous, no user stored in database) authentication.
	//The get credentialsCallback provided is automatically called by the library whenever authentication is required (during connection/reconnection)
	// It returns a task to enable you to return credential asynchronously.
	// please note that if platform plugins are installed, they automatically provide credentials.
	users->getCredentialsCallback = []() {
		Stormancer::Users::AuthParameters authParameters;
		authParameters.type = "ephemeral";
		return pplx::task_from_result(authParameters);
	};
	state = AgentState::InQueue;
	return users->login().then([&state](pplx::task<void> t) {
		try
		{
			
			t.get();
			state = AgentState::Connected;
			return  true;
		}
		catch (std::exception&)
		{
			state = AgentState::Disconnected;
			return false;
		}
	});
}
struct Stats
{
	int maxInQueue;
	int maxCcu;
};
int getConnectedAgent(AgentState state[], int ranks[], int length, Stats& stats)
{
	int result = -1;
	int currentCcu = 0;
	int currentInQueue = 0;
	for (int i = 0; i < length; i++)
	{
		auto client = Stormancer::IClientFactory::GetClient(i);
		auto queue = client->dependencyResolver().resolve<Stormancer::Limits::ConnectionQueue>();
		if (state[i] == AgentState::Connected)
		{
			//Only one connected
			currentCcu++;

			
			ranks[i] = -1;
			result = i;
		}
		else if (queue->isInQueue() && queue->getRank() != -1)
		{
			currentInQueue++;
		}
	}

	if (stats.maxCcu < currentCcu)
	{
		stats.maxCcu = currentCcu;
	}
	if (stats.maxInQueue < currentInQueue)
	{
		stats.maxInQueue = currentInQueue;
	}
	return result;
}
void disconnectAgent(int id, AgentState& state)
{
	state = AgentState::Disconnecting;
	auto client = Stormancer::IClientFactory::GetClient(id);
	client->disconnect().then([&state](pplx::task<void> t) 
	{
		state = AgentState::Disconnected;
		try
		{
			t.get();
		}
		catch (std::exception&)
		{
			//Continuations must catch all errors.
		}
	});
}
TEST(GameFlow, AuthenticateWithQueue) {
	
	//Create an action dispatcher to dispatch callbacks and continuation in the thread running the method.
	auto dispatcher = std::make_shared<Stormancer::MainThreadActionDispatcher>();

	//Create a configurator used for all clients.
	Stormancer::IClientFactory::SetDefaultConfigurator([dispatcher](size_t id) {

		//Create a configuration that connects to the test application.
		auto config = Stormancer::Configuration::create(std::string(ServerEndpoint), std::string(Account), std::string(Application));
		//config->logger = std::make_shared<Stormancer::FileLogger>(std::to_string(id), std::to_string(id) + ".logs.txt");
		//Add plugins required by the test.
		config->addPlugin(new Stormancer::Users::UsersPlugin());
		config->addPlugin(new Stormancer::Limits::ConnectionQueuePlugin());
		config->actionDispatcher = dispatcher;
		return config;
	});

	const int nbAgents = 5;
	AgentState agentStates[nbAgents];
	int ranks[nbAgents];

	std::vector<pplx::task<bool>> tasks;

	for (int i = 0; i < nbAgents; i++)
	{
		tasks.push_back(runAgent(i, agentStates[i]));
	}
	auto t = pplx::when_all(tasks.begin(), tasks.end());

	Stats stats;
	//loop until test is completed and run library events.
	while (!t.is_done())
	{
		//Runs the  callbacks and continuations waiting to be executed (mostly user code) for max 5ms.
		dispatcher->update(std::chrono::milliseconds(5));
		std::this_thread::sleep_for(std::chrono::milliseconds(10));

		auto connectedAgent = getConnectedAgent(agentStates,ranks, nbAgents,stats);

		
		if (connectedAgent != -1)
		{
			disconnectAgent(connectedAgent, agentStates[connectedAgent]);
		}
		
	}
	for (int i = 0; i < nbAgents; i++)
	{
		Stormancer::IClientFactory::ReleaseClient(i);
	
	}
	EXPECT_TRUE(stats.maxCcu <= 5);
	EXPECT_TRUE(stats.maxInQueue <= 10);

	/*for (int i = 0; i < nbAgents; i++)
	{
		EXPECT_TRUE(tasks[i].get());
	}*/
}
