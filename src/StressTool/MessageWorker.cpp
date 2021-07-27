#include "Worker.h"
#include "Timer.h"
//Provides a way to store end easily access client instances.
#include "stormancer/IClientFactory.h"
#include "stormancer/Logger/VisualStudioLogger.h"
//Provides APIs related to authentication & user management.
#include "Users/Users.hpp"

constexpr const char* ServerEndpoint = "http://localhost";//"http://gc3.stormancer.com";
constexpr const char* Account = "tests";
constexpr const char* Application = "test";

pplx::task<StressTool::Result> StressTool::MessagesWorker::run(int id)
{
	auto timer = std::make_shared<Timer>();
	//Create a configuration associated with the client of id 0.
	Stormancer::IClientFactory::SetConfig(id, [](size_t) {

		//Create a configuration that connects to the test application.
		auto config = Stormancer::Configuration::create(std::string(ServerEndpoint), std::string(Account), std::string(Application));
		//config->logger = std::make_shared<Stormancer::VisualStudioLogger>();
		//Add plugins required by the test.
		config->addPlugin(new Stormancer::Users::UsersPlugin());


		return config;
	});

	//Gets client with id 0.
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

	//Login manually. Note that calling other APIs automatically performs login if necessary, 
	//so call this method to login earlier, for instance during game or online menu loading as a form of "preload".


	timer->start();
	//login() returns an asynchronous task, which calls the continuation function specified as argument of then() when it is completed.
	// t.get() blocks until completion 
	return users->login().then([timer, id](pplx::task<void> t) {
		Stormancer::IClientFactory::ReleaseClient(id);
		Result r;
		r.duration = timer->getElapsedTimeInMilliSec();
		try
		{

			t.get();
			r.success = true;

		}
		catch (std::exception& ex)
		{
			std::cout << ex.what();
			r.success = false;
		}
		return r;
	});

}
