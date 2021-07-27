#include "stormancer/Tasks.h"

namespace StressTool
{
	struct Result
	{
		bool success;
		double duration;
	};
	class Worker
	{
	public:
		/// <summary>
		/// Runs the test and returns the time it took in microseconds
		/// </summary>
		/// <param name="id"></param>
		/// <returns></returns>
		virtual pplx::task<Result> run(int id) = 0;
	};

	class ConnectionWorker : public Worker
	{
	public:
		virtual pplx::task<Result> run(int id) override;
	};

	class MessagesWorker : public Worker
	{
	public:
		virtual pplx::task<Result> run(int id) override;
	};
}