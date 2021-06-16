using System;
using System.IO;

namespace workspace_handler_lambda_function
{
    class Program
    {
        static void Main(string[] args)
        {
            // Display the number of command line arguments.
            Console.WriteLine(args.Length);

            var lambdaFxn = new Function();

            var s3Event = File.ReadAllText("test/s3Event_input.json");

            var lambdaOutput = lambdaFxn.FunctionHandler(s3Event, null);

            Console.WriteLine(lambdaOutput);
        }
    }
}
