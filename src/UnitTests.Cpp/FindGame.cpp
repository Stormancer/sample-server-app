#include "pch.h"

//Provides a way to store end easily access client instances.
#include "stormancer/IClientFactory.h"

//Provides APIs related to authentication & user management.
#include "Users/Users.hpp"

//Provides APIs related to player parties.
#include "Party/Party.hpp"

//Declares MainThreadActionDispatcher, a class that enables the dev to run stormancer callbacks & continuations on the thread of their choice.
#include "stormancer/IActionDispatcher.h"
#include "stormancer/Logger/FileLogger.h"

constexpr  char* ServerEndpoint = "http://localhost";//"http://gc3.stormancer.com";
constexpr  char* Account = "tests";
constexpr  char* Application = "test";

pplx::task<bool> FindGameImpl(int id)
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

	auto gameFinder = client->dependencyResolver().resolve<Stormancer::GameFinder::GameFinderApi>();
	auto party = client->dependencyResolver().resolve<Stormancer::Party::PartyApi>();

	//Create a task that will complete the next time a game is found.
	auto gameFoundTask = gameFinder->waitGameFound();

	

	Stormancer::Party::PartyRequestDto request;
	request.GameFinderName = "matchmaking";
	//Name of the matchmaking, defined in Stormancer.Server.TestApp/TestPlugin.cs.
	//>  host.AddGamefinder("matchmaking", "matchmaking");

	return party->createPartyIfNotJoined(request)
	.then([client]()
	{
		auto party = client->dependencyResolver().resolve<Stormancer::Party::PartyApi>();

		//Triggers matchmking by setting the player as ready.
		//Matchmaking starts when all players in the party are ready.
		return party->updatePlayerStatus(Stormancer::Party::PartyUserStatus::Ready);
	})
	.then([gameFoundTask]()
	{
		return gameFoundTask;
	})
	.then([id](pplx::task<Stormancer::GameFinder::GameFoundEvent> t)
	{
		try
		{

			//Stormancer::IClientFactory::ReleaseClient(id);
			t.get();
			return true;
		}
		catch (std::exception&)
		{

			return false;
		}
	});


}

TEST(GameFlow, FindGame) {

	//Create an action dispatcher to dispatch callbacks and continuation in the thread running the method.
	auto dispatcher = std::make_shared<Stormancer::MainThreadActionDispatcher>();

	//Create a configurator used for all clients.
	Stormancer::IClientFactory::SetDefaultConfigurator([dispatcher](size_t id) {

		//Create a configuration that connects to the test application.
		auto config = Stormancer::Configuration::create(std::string(ServerEndpoint), std::string(Account), std::string(Application));
		config->logger = std::make_shared<Stormancer::FileLogger>(std::to_string(id),std::to_string(id)+".logs.txt");
		//Add plugins required by the test.
		config->addPlugin(new Stormancer::Users::UsersPlugin());
		config->addPlugin(new Stormancer::Party::PartyPlugin());
		config->addPlugin(new Stormancer::GameFinder::GameFinderPlugin());
		//config->addPlugin(new Stormancer::GameSessions::GameSessionsPlugin());

		config->actionDispatcher = dispatcher;
		return config;
	});
	
	

	std::vector<pplx::task<bool>> tasks;
	tasks.push_back(FindGameImpl(0));
	tasks.push_back(FindGameImpl(1));
	auto t = pplx::when_all(tasks.begin(),tasks.end());

	//loop until test is completed and run library events.
	while (!t.is_done())
	{
		//Runs the  callbacks and continuations waiting to be executed (mostly user code) for max 5ms.
		dispatcher->update(std::chrono::milliseconds(5));
		std::this_thread::sleep_for(std::chrono::milliseconds(10));
	}

	Stormancer::IClientFactory::ReleaseClient(0);
	Stormancer::IClientFactory::ReleaseClient(1);
	for (auto t : tasks)
	{
		EXPECT_TRUE(t.get());
	}

	

}