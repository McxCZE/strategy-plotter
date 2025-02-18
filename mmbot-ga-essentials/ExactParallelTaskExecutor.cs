﻿using GeneticSharp.Infrastructure.Framework.Threading;
using log4net;

namespace MMBotGA.ga.execution
{
    public class ExactParallelTaskExecutor : ParallelTaskExecutor
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(ExactParallelTaskExecutor));

        private readonly int _degreeOfParallelism;

        public ExactParallelTaskExecutor(int degreeOfParallelism)
        {
            _degreeOfParallelism = degreeOfParallelism;
        }

        public override bool Start()
        {
            try
            {
                var queue = new Queue<Action>(Tasks);
                var awaits = new List<Task>();
                var semaphore = new SemaphoreSlim(_degreeOfParallelism);
                while (queue.Any())
                {
                    semaphore.Wait();
                    var action = queue.Dequeue();
                    awaits.Add(Task.Run(() =>
                    {
                        try
                        {
                            for (var i = 0; i < 3; i++)
                            {
                                try
                                {
                                    action();
                                    return;
                                }
                                catch (Exception e)
                                {
                                    Log.Error($"Exception while executing GA action. Will retry ({2 - i}).", e);
                                }
                            }
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    }));
                }
                Task.WaitAll(awaits.ToArray());

                return true;
            }
            finally
            {
                IsRunning = false;
            }
        }
    }
}
