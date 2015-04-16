﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

using ExchangeService.Exceptions;
using ExchangeService.UserClasses;

using NUnit.Framework;

using RemoteQueue.Cassandra.Entities;
using RemoteQueue.Handling;

using SKBKontur.Catalogue.RemoteTaskQueue.TaskDatas;

namespace FunctionalTests.ExchangeTests
{
    public class AutoParentTaskIdTest : FunctionalTestBase
    {
        public override void SetUp()
        {
            base.SetUp();
            remoteTaskQueue = Container.Get<IRemoteTaskQueue>();
            testTaskLogger = Container.Get<ITestTaskLogger>();
        }

        [TestCase(1, TestName = "TestOneChain")]
        [TestCase(10, TestName = "TestTenChains")]
        [TestCase(100, TestName = "TestManyChains")]
        public void Test(int chainsCount)
        {
            var loggingId = Guid.NewGuid().ToString();
            Console.WriteLine("Start test. LoggingId = {0}", loggingId);
            for(var i = 0; i < chainsCount; i++)
            {
                remoteTaskQueue.CreateTask(new ChainTaskData
                    {
                        LoggingTaskIdKey = loggingId,
                        ChainName = string.Format("Chain {0}", i),
                        ChainPosition = 0
                    }).Queue();
            }
            var taskInfos = WaitLoggedTasks(loggingId, chainsCount * tasksInChain, TimeSpan.FromMinutes(2));
            CheckChains(taskInfos);
        }

        private const int tasksInChain = 10;

        private void CheckChains(RemoteTaskInfo<ChainTaskData>[] infos)
        {
            Console.WriteLine("Start checking chains");
            var chains = infos
                .GroupBy(info => info.TaskData.ChainName).ToArray();
            Assert.That(chains.Length, Is.EqualTo(infos.Length / tasksInChain), 
                string.Format("Количество цепочек должно быть равно общему числу тасков, деленному на {0}", tasksInChain));
            Console.WriteLine("Found {0} chains, as expected", chains.Length);
            foreach(var grouping in chains)
                CheckChain(grouping.Key, grouping.ToArray());
            Console.WriteLine("Checking chains success");
        }

        private void CheckChain(string chainName, RemoteTaskInfo<ChainTaskData>[] chain)
        {
            Console.WriteLine("Start check chain '{0}'", chainName);
            Assert.That(chain.Length, Is.EqualTo(tasksInChain), string.Format("Количество задач в цепочке должно быть равно {0}", tasksInChain));
            var ordered = chain.OrderBy(info => info.TaskData.ChainPosition).ToArray();
            string previousTaskId = null;
            foreach(var taskInfo in ordered)
            {
                Assert.That(taskInfo.Context.ParentTaskId, Is.EqualTo(previousTaskId), string.Format("Не выполнилось ожидание правильного ParentTaskId для таски {0}", taskInfo.Context.Id));
                previousTaskId = taskInfo.Context.Id;
            }
            Console.WriteLine("Check chain '{0}' success", chainName);
        }

        private RemoteTaskInfo<ChainTaskData>[] WaitLoggedTasks(string loggingId, int expectedTasks, TimeSpan timeout)
        {
            const int sleepInterval = 5000;
            var stopwatch = Stopwatch.StartNew();
            while(true)
            {
                if (stopwatch.Elapsed > timeout)
                    throw new TooLateException("Время ожидания превысило {0} мс.", timeout);
                var ids = testTaskLogger.GetAll(loggingId);
                if (ids.Length < expectedTasks)
                {
                    Console.WriteLine("Read {0} tasks, expected {1} tasks. Sleep", ids.Length, expectedTasks);
                    Thread.Sleep(sleepInterval);
                    continue;
                }
                if (ids.Length > expectedTasks)
                    throw new Exception(string.Format("Found {0} tasks, when expected {1} tasks", ids.Length, expectedTasks));
                Console.WriteLine("Found {0} tasks, as expected", ids.Length);
                var taskInfos = remoteTaskQueue.GetTaskInfos<ChainTaskData>(ids);
                var finished = taskInfos.Where(info => info.Context.State == TaskState.Finished).ToArray();
                var notFinished = taskInfos.Where(info => info.Context.State != TaskState.Finished).ToArray();
                if(notFinished.Length != 0)
                {
                    Console.WriteLine("Found {0} finished tasks, but {1} not finished. Sleep", finished.Length, notFinished.Length);
                    Thread.Sleep(sleepInterval);
                    continue;
                }
                Console.WriteLine("Found {0} finished tasks, as expected. Finish waiting", finished.Length);
                return taskInfos;
            }
        }

        private IRemoteTaskQueue remoteTaskQueue;
        private ITestTaskLogger testTaskLogger;
    }
}