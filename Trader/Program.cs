﻿using Autofac;
using BinanceExchange.API.Client;
using BinanceExchange.API.Client.Interfaces;
using FluentScheduler;
using System;
using System.Linq;
using Trader.Broker;
using Trader.Exchange;
using Trader.Networking;
using Trader.Reporter;
using Trader.Time;

namespace Trader
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Starting up");
            try
            {
                var config = new Config();

                var registry = new Registry();
                registry.Schedule(() => config.Reload()).ToRunEvery(30).Seconds();
                JobManager.Initialize(registry);
                JobManager.JobException += (exInfo) => { throw exInfo.Exception; };

                using (var container = ConfigureDependencies(config))
                {
                    var trader = container.Resolve<Trader>();
                    trader.Initialize().Wait();
                    while (true)
                    {
                        trader.Trade().Wait();
                    }
                }
            }
            catch (Exception e)
            {
                while (e != null)
                {
                    Console.WriteLine($"EXCEPTION: {e.Message}\n{e.StackTrace}");
                    e = e.InnerException;
                }
                Console.ReadLine();
            }
            finally
            {
                JobManager.StopAndBlock();
            }
        }

        private static IContainer ConfigureDependencies(Config config)
        {
            var builder = new ContainerBuilder();

            builder.RegisterType<Trader>();

            builder.RegisterInstance(config);
            builder.RegisterInstance(new ClientConfiguration() { ApiKey = config.ApiKey, SecretKey = config.ApiKeySecret });

            builder.RegisterType<WebSocket>().As<IWebSocket>();
            builder.RegisterType<BinanceClient>().As<IBinanceClient>();
            builder.RegisterType<UtcTime>().As<ITime>();

            Type exchangeType = typeof(Program).Assembly.GetTypes().Where(t => t.IsAssignableTo<IExchange>()).FirstOrDefault((t) =>
            {
                var attr = t.GetCustomAttributes(true).OfType<ExchangeTypeAttribute>().FirstOrDefault();
                return attr != null && attr.Exchange == config.Exchange;
            });
            builder.RegisterType(exchangeType).As<IExchange>();

            Type brokerType = typeof(Program).Assembly.GetTypes().Where(t => t.IsAssignableTo<IBroker>()).FirstOrDefault((t) =>
            {
                var attr = t.GetCustomAttributes(true).OfType<BrokerTypeAttribute>().FirstOrDefault();
                return attr != null && attr.Broker == config.Broker;
            });
            builder.RegisterType(brokerType).As<IBroker>();

            Type reporterType = typeof(Program).Assembly.GetTypes().Where(t => t.IsAssignableTo<IReporter>()).FirstOrDefault((t) =>
            {
                var attr = t.GetCustomAttributes(true).OfType<ReporterTypeAttribute>().FirstOrDefault();
                return attr != null && attr.Reporter == config.Reporter;
            });
            builder.RegisterType(reporterType).As<IReporter>();

            return builder.Build();
        }
    }
}
