#include "pch.h"

//Provides a way to store end easily access client instances.
#include "stormancer/IClientFactory.h"

//Provides APIs related to authentication & user management.
#include "Users/Users.hpp"

#include "InAppNotification/Notifications.hpp"
#include "stormancer/cpprestsdk/cpprest/http_client.h"

#include "stormancer/Logger/FileLogger.h"

//Declares MainThreadActionDispatcher, a class that enables the dev to run stormancer callbacks & continuations on the thread of their choice.
#include "stormancer/IActionDispatcher.h"

constexpr  char* ServerEndpoint = "http://localhost";//"http://gc3.stormancer.com";
constexpr  char* Account = "tests";
constexpr  char* Application = "test";



TEST(GameFlow, ReceiveNotification) {

	//Create an action dispatcher to dispatch callbacks and continuation in the thread running the method.
	auto dispatcher = std::make_shared<Stormancer::MainThreadActionDispatcher>();

	//Create a configuration associated with the client of id 0.
	Stormancer::IClientFactory::SetConfig(0, [dispatcher](size_t) {

		//Create a configuration that connects to the test application.
		auto config = Stormancer::Configuration::create(std::string(ServerEndpoint), std::string(Account), std::string(Application));
		config->logger = std::make_shared<Stormancer::FileLogger>(std::to_string(0), std::to_string(0) + ".logs.txt");

		//Add plugins required by the test.
		config->addPlugin(new Stormancer::Users::UsersPlugin());
		config->addPlugin(new Stormancer::Notifications::NotificationsPlugin());
		//config->addPlugin(new Stormancer::Party::PartyPlugin());
		//config->addPlugin(new Stormancer::GameFinder::GameFinderPlugin());
		//config->addPlugin(new Stormancer::GameSessions::GameSessionsPlugin());

		config->actionDispatcher = dispatcher;
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

	bool testCompleted = false;
	bool testSucceeded = false;


	auto notifications = client->dependencyResolver().resolve<Stormancer::Notifications::NotificationsApi>();

	
	pplx::task_completion_event<void> tce;

	//Keep a ref to the subscription object returned by subscribe to stay subscribed to the event.
	auto subscription = notifications->subscribe([tce](std::vector<Stormancer::Notifications::InAppNotification> notifications) {

		tce.set();
	});

	//login() returns an asynchronous task, which calls the continuation function specified as argument of then() when it is completed.
	// t.get() blocks until completion 
	users->login()
		.then([tce]() {return pplx::create_task(tce); })
		.then([&testCompleted, &testSucceeded](pplx::task<void> t) {
		try
		{
			testCompleted = true;
			t.get();
			testSucceeded = true;
		}
		catch (std::exception&)
		{
			testSucceeded = false;
		}
	});

	bool notificationSent = false;
	

	while (!testCompleted)
	{
		//Runs the  callbacks and continuations waiting to be executed (mostly user code) for max 5ms.
		dispatcher->update(std::chrono::milliseconds(5));
		if (!notificationSent && users->connectionState() == Stormancer::Users::GameConnectionState::Authenticated)
		{
			notificationSent = true;
			Stormancer::web::http::client::http_client httpClient(L"http://localhost:81");
			auto body = Stormancer::web::json::value::object();
			body[L"userIds"] = Stormancer::web::json::value::string(L"*");
			body[L"type"] = Stormancer::web::json::value::string(L"customType");
			body[L"message"] = Stormancer::web::json::value::string(L"A notification message");
			body[L"data"] = Stormancer::web::json::value::string(L"custom_data");
			httpClient.request(Stormancer::web::http::methods::POST, L"_app/tests/test/_admin/_notifications/send", body);

		}
	}


	Stormancer::IClientFactory::ReleaseClient(0);
	EXPECT_TRUE(testSucceeded);

}
