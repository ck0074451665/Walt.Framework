using System;
using System.Collections.Specialized;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;  
using Walt.Framework.Log;
using Walt.Framework.Service;
using Walt.Framework.Configuration;

using Quartz;
using Quartz.Impl;
using Quartz.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace QuartzSampleApp
{
    public class Program
    {

        private ILoggerFactory _loggerFact;
        public static void Main(string[] args)
        {
            

            var host = new HostBuilder()
                    .UseEnvironment(EnvironmentName.Development)
                    .ConfigureAppConfiguration((hostContext,configApp)=>{ 
                         configApp.SetBasePath(Directory.GetCurrentDirectory());
                         configApp.AddJsonFile(
                               $"appsettings.{hostContext.HostingEnvironment.EnvironmentName}.json", 
                                  optional: true);
                          configApp.AddEnvironmentVariables(prefix: "PREFIX_");
                         configApp.AddCommandLine(args);
                    }).ConfigureLogging((hostContext,configBuild)=>{
                         configBuild.AddConfiguration(hostContext.Configuration.GetSection("Logging"));
                         configBuild.AddDebug();
                         configBuild.AddCustomizationLogger();
                    })
                    .ConfigureServices((hostContext,service)=>{
                          service.AddKafka(KafkaBuilder=>{
                             KafkaBuilder.AddConfiguration(hostContext.Configuration.GetSection("KafkaService"));
                         });
                    })
                    .Build();
            
            ILoggerFactory loggerFact=host.Services.GetService<ILoggerFactory>();
            LogProvider.SetCurrentLogProvider(new ConsoleLogProvider(loggerFact));
            var ischema=RunProgramRunExample(loggerFact);
            ischema.GetAwaiter().GetResult();
            host.Run();
            ischema.Result.Shutdown();
        }

        private static async Task<IScheduler> RunProgramRunExample(ILoggerFactory loggerFact)
        {
            var log=loggerFact.CreateLogger<Program>();
            try
            {
                // Grab the Scheduler instance from the Factory
                NameValueCollection props = new NameValueCollection
                {
                    { "quartz.serializer.type", "binary" }
                };
                StdSchedulerFactory factory = new StdSchedulerFactory(props);
                IScheduler scheduler = await factory.GetScheduler();

                // and start it off
                await scheduler.Start();

                // define the job and tie it to our HelloJob class
                IJobDetail job = JobBuilder.Create<HelloJob>()
                    .WithIdentity("job1", "group1")
                    .Build();

                // Trigger the job to run now, and then repeat every 10 seconds
                ITrigger trigger = TriggerBuilder.Create()
                    .WithIdentity("trigger1", "group1")
                    .StartNow()
                    .WithSimpleSchedule(x => x
                        .WithIntervalInSeconds(10)
                        .RepeatForever())
                    .Build();

                // Tell quartz to schedule the job using our trigger
                await scheduler.ScheduleJob(job, trigger);

                // some sleep to show what's happening
                //await Task.Delay(TimeSpan.FromSeconds(60));

                // and last shut down the scheduler when you are ready to close your program
                return scheduler;
            }
            catch (SchedulerException se)
            {
               log.LogError(se,"job执行错误。");
            }
            return null;
        }

        // simple log provider to get something to the console
        private class ConsoleLogProvider : ILogProvider
        {
            private ILoggerFactory _logFactory;

            public ConsoleLogProvider(ILoggerFactory logFactory)
            {
                _logFactory=logFactory;
            }
            public Logger GetLogger(string name)
            {
                return (level, func, exception, parameters) =>
                {
                    if (func != null)
                    {
                        string logInfo=string.Format("[" + DateTime.Now.ToLongTimeString() 
                        + "] [" + level + "] " + func(), parameters);
                        var log=_logFactory.CreateLogger<ConsoleLogProvider>();
                        log.LogDebug(logInfo);
                    }
                    return true;
                };
            }

            public IDisposable OpenNestedContext(string message)
            {
                throw new NotImplementedException();
            }

            public IDisposable OpenMappedContext(string key, string value)
            {
                throw new NotImplementedException();
            }
        }
    }

    public class HelloJob : IJob
    {
        public async Task Execute(IJobExecutionContext context)
        {
            await Console.Out.WriteLineAsync("Greetings from HelloJob!");
        }
    }
}