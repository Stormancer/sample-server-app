#include "pch.h"
#include "stormancer/IClientFactory.h"
#include "Users/Users.hpp"
#include "Party/Party.hpp"
#include "GameFinder/GameFinder.hpp"
#include "GameSession/Gamesessions.hpp"

constexpr  char* ServerEndpoint = "http://localhost";//"http://gc3.stormancer.com";
constexpr  char* Account = "tests";
constexpr  char* Application = "test";


TEST(GameFlow, Authenticate) {

	//Create a configuration associated with the client of id 0.
	Stormancer::IClientFactory::SetConfig(0, []() {

		//Create a configuration that connects to the test application.
		auto config = Stormancer::Configuration::create(std::string(ServerEndpoint), std::string(Account), std::string(Application));
		
		//Add plugins required by the test.
		config->addPlugin(new Stormancer::Users::UsersPlugin());
		//config->addPlugin(new Stormancer::Party::PartyPlugin());
		//config->addPlugin(new Stormancer::GameFinder::GameFinderPlugin());
		//config->addPlugin(new Stormancer::GameSessions::GameSessionsPlugin());

		return config;
	});

	//Gets client with id 0.
	auto client = Stormancer::IClientFactory::GetClient(0);

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

	//Login manually. Note that calling other APIs automatically performs login if necessary, 
	//so call this method to login earlier, for instance during game or online menu loading as a form of "preload".
	auto loginTask = users->login();

	//Login returns an asynchronous task. Call get() to block the current thread until completion and get the result.
	// .get() throws if an error occured.
	//call .then() to specify a continuation which will be executed on completion.
	try
	{
		loginTask.get();
		EXPECT_TRUE(true);
	}
	catch (std::exception&)
	{
		EXPECT_TRUE(false);
	}

	/*EXPECT_EQ(1, 1);
	EXPECT_TRUE(true);*/
}
