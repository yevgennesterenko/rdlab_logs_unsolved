using System;
using System.Reactive.Linq;
using MessagePersister.Implementation;
using MessagePersister.Interfaces;
using System.Threading;
using log4net;
using log4net.Config;
using log4net.Repository.Hierarchy;
using Serilog;

namespace MessageProcessorApplication
{
    


    class Program
    {
        private static ILog logger = LogManager.GetLogger(typeof(Program));
        private static ILogger seriLogger;

        public static IDisposable GenerateRandomMessagesAtRandomIntervalsFromOneBus(IMessagePersister persister)
        {
            var source = SolaceBusMock("SOLACE.MESSAGEBUS.ONE", 10, 100);

            var subscription = source.Subscribe(x => ProcessMessages(x, persister));
            return subscription;
        }


        public static void GenerateRandomMessagesAtRandomIntervalsFromSeveralBusesHighLoad(IMessagePersister persister, out IDisposable subscription1, out IDisposable subscription2, out IDisposable subscription3)
        {
            var source1 = SolaceBusMock("SOLACE.MESSAGEBUS.ONE.SLOW", 30, 500);
            var source2 = SolaceBusMock("SOLACE.MESSAGEBUS.ONE.HIGH", 100, 10);
            var source3 = SolaceBusMock("SOLACE.MESSAGEBUS.ONE.MEDIUM", 79, 30);

            subscription1 = source1.Subscribe(x => ProcessMessages(x, persister));
            subscription2 = source2.Subscribe(x => ProcessMessages(x, persister));
            subscription3 = source3.Subscribe(x => ProcessMessages(x, persister));
        }

        //Solace bus mock - produce observable collection of random messages which comes at random intervals 
        private static IObservable<Message> SolaceBusMock(string solaceBusName, int numberOfMessages, int timeIntervalMilliseconds)
        {
            Random rnd = new Random();
            var source = Observable.Generate(0, x => x < numberOfMessages, x => x + 1, _ => CreateMessage(solaceBusName, rnd),
                _ => TimeSpan.FromMilliseconds(rnd.Next(1, 10) * timeIntervalMilliseconds));
            return source;
        }

        private static Message CreateMessage(string solaceBusName, Random rnd)
        {
            var guid = Guid.NewGuid();
            return new Message(guid, String.Format("Message {0} from bus {1}", guid, solaceBusName),
                rnd.Next(0, 10).ToString());
        }


        private static void ProcessMessages(Message message, IMessagePersister persister)
        {
            Console.WriteLine(message.Name);
            //log it
            logger.InfoFormat("Message persisted: {0}", message.Name);
            logger.DebugFormat("Debug Message persisted: {0}", message.Timestamp);
            seriLogger.Information("Message persisted", message.Name);
            seriLogger.Information("Message persisted {@Message}", message);
            persister.Persist(message);
        }

        static void Main(string[] args)
        {
            XmlConfigurator.Configure();
            var appenders = LogManager.GetLogger(logger.GetType()).Logger.Repository.GetAppenders();
            seriLogger = new LoggerConfiguration().WriteTo.ColoredConsole().WriteTo.RollingFile(@"C:\Logs\Log-{Date}.txt").CreateLogger();

            //case 1
            IMessagePersister persister = new AsyncMessagePersister();
            var subscription = GenerateRandomMessagesAtRandomIntervalsFromOneBus(persister);
            Thread.Sleep(3000);
            persister.Stop();
            Thread.Sleep(3000);
            subscription.Dispose();
            Console.WriteLine("Press enter to proceed to case 2");
            Console.ReadLine();

            //case 2
            IMessagePersister anotherPersister = new AsyncMessagePersister();
            IDisposable subscription1, subscription2, subscription3;
            GenerateRandomMessagesAtRandomIntervalsFromSeveralBusesHighLoad(anotherPersister, out subscription1, out subscription2, out subscription3);
            Thread.Sleep(1000);
            anotherPersister.StopImmediately();
            subscription1.Dispose();
            subscription2.Dispose();
            subscription3.Dispose();
            Console.WriteLine("Press enter to exit");
            Console.ReadLine();
        }


    }
}
