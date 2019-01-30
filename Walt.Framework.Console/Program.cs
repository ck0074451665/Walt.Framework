using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using EnvironmentName = Microsoft.Extensions.Hosting.EnvironmentName;
using Walt.Framework.Log;
using Walt.Framework.Service;
using Walt.Framework.Service.Kafka;
using Walt.Framework.Configuration;
using MySql.Data.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.Threading;
using IApplicationLife =Microsoft.Extensions.Hosting;
using IApplicationLifetime = Microsoft.Extensions.Hosting.IApplicationLifetime;

namespace Walt.Framework.Console
{
    public class Program
    {
        public static void Main(string[] args)
        {
            //这里获取程序及和工作线程配置信息
            Dictionary<string, Assembly> assmblyColl = new Dictionary<string, Assembly>();
            var host = new HostBuilder()
                    .UseEnvironment(EnvironmentName.Development)
                
                    .ConfigureAppConfiguration((hostContext, configApp) =>
                    {
                        //这里netcore支持多数据源，所以可以扩展到数据库或者redis，集中进行配置。
                        //
                        configApp.SetBasePath(Directory.GetCurrentDirectory());
                        configApp.AddJsonFile(
                              $"appsettings.{hostContext.HostingEnvironment.EnvironmentName}.json",
                                 optional: true);
                        configApp.AddEnvironmentVariables("PREFIX_");
                        configApp.AddCommandLine(args);
                    }).ConfigureLogging((hostContext, configBuild) =>
                    {
                        configBuild.AddConfiguration(hostContext.Configuration.GetSection("Logging"));
                        configBuild.AddConsole();
                        configBuild.AddCustomizationLogger();
                    })
                    .ConfigureServices((hostContext, service) =>
                    {
                        service.Configure<HostOptions>(option =>
                        {
                            option.ShutdownTimeout = System.TimeSpan.FromSeconds(10);
                        });

                        service.AddKafka(KafkaBuilder =>
                        {
                            KafkaBuilder.AddConfiguration(hostContext.Configuration.GetSection("KafkaService"));
                        });
                        service.AddElasticsearchClient(config=>{
                            config.AddConfiguration(hostContext.Configuration.GetSection("ElasticsearchService"));
                        });

                        service.AddDbContext<ConsoleDbContext>(option =>
                        option.UseMySQL(hostContext.Configuration.GetConnectionString("ConsoleDatabase")), ServiceLifetime.Transient, ServiceLifetime.Transient);
                        ///TODO 待实现从数据库中pull数据，再将任务添加进DI
                        service.AddSingleton<IConsole,KafkaToElasticsearch>();
                    })
                    .Build();
             CancellationTokenSource source = new CancellationTokenSource();
            CancellationToken token = source.Token;
            var task=Task.Run(async () =>{
                IConsole console = host.Services.GetService<IConsole>();
                await console.AsyncExcute(source.Token);
            },source.Token);
            Dictionary<string, Task> dictTask = new Dictionary<string, Task>();
            dictTask.Add("kafkatoelasticsearch", task);

            int recordRunCount = 0;
            var fact = host.Services.GetService<ILoggerFactory>();
            var log = fact.CreateLogger<Program>();
            var disp = Task.Run(() =>
            {
                while (true)
                {
                    if (!token.IsCancellationRequested)
                    {
                        ++recordRunCount;
                        foreach (KeyValuePair<string, Task> item in dictTask)
                        {
                            if (item.Value.IsCanceled
                            || item.Value.IsCompleted
                            || item.Value.IsCompletedSuccessfully
                            || item.Value.IsFaulted)
                            {
                                log.LogWarning("console任务：{0}，参数：{1}，执行异常,task状态：{2}", item.Key, "", item.Value.Status);
                                if (item.Value.Exception != null)
                                {
                                    log.LogError(item.Value.Exception, "task:{0},参数：{1}，执行错误.", item.Key, "");
                                    //TODO 根据参数更新数据库状态，以便被监控到。
                                }
                                //更新数据库状态。
                            }
                        }
                    }
                    System.Threading.Thread.Sleep(2000);
                    log.LogInformation("循环：{0}次，接下来等待2秒。", recordRunCount);
                }
            },source.Token);
            
            IApplicationLifetime appLiftTime = host.Services.GetService<IApplicationLifetime>();
            appLiftTime.ApplicationStopping.Register(()=>{
                log.LogInformation("程序停止中。");
                source.Cancel();
                log.LogInformation("程序停止完成。");
            });
            host.RunAsync().GetAwaiter().GetResult();
        }
    }
}
