#include "pch.h"

//Provides a way to store end easily access client instances.
#include "stormancer/IClientFactory.h"


//Declares MainThreadActionDispatcher, a class that enables the dev to run stormancer callbacks & continuations on the thread of their choice.
#include "stormancer/IActionDispatcher.h"

constexpr const char* ServerEndpoint = "http://localhost";//"http://gc3.stormancer.com";
constexpr const char* Account = "tests";
constexpr const char* Application = "test";


TEST(GameFlow, RejectConnection) {

	//Create an action dispatcher to dispatch callbacks and continuation in the thread running the method.
	auto dispatcher = std::make_shared<Stormancer::MainThreadActionDispatcher>();

	//Create a configuration associated with the client of id 0.
	Stormancer::IClientFactory::SetConfig(0, [dispatcher](size_t) {

		//Create a configuration that connects to the test application.
		auto config = Stormancer::Configuration::create(std::string(ServerEndpoint), std::string(Account), std::string(Application));

		//Add plugins required by the test.
		//config->addPlugin(new Stormancer::Users::UsersPlugin());
		//config->addPlugin(new Stormancer::Party::PartyPlugin());
		//config->addPlugin(new Stormancer::GameFinder::GameFinderPlugin());
		//config->addPlugin(new Stormancer::GameSessions::GameSessionsPlugin());

		config->actionDispatcher = dispatcher;
		return config;
		});

	//Gets client with id 0.
	auto client = Stormancer::IClientFactory::GetClient(0);

	

	
	bool testCompleted = false;
	bool testSucceeded = false;

	//login() returns an asynchronous task, which calls the continuation function specified as argument of then() when it is completed.
	// t.get() blocks until completion 
	client->connectToPublicScene("rejection-test-scene").then([&testCompleted, &testSucceeded](pplx::task <std::shared_ptr<Stormancer::Scene>> t) {
		try
		{
			testCompleted = true;
			t.get();
			testSucceeded = false;
		}
		catch (std::exception& ex)
		{
			testSucceeded = ex.what() == "reject";
			
		}
		});

	while (!testCompleted)
	{
		//Runs the  callbacks and continuations waiting to be executed (mostly user code) for max 5ms.
		dispatcher->update(std::chrono::milliseconds(5));
	}


	Stormancer::IClientFactory::ReleaseClient(0);
	EXPECT_TRUE(testSucceeded);

}
