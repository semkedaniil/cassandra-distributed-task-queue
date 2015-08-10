﻿using System;
using System.Linq;

using Kontur.Tracing.Core;
using Kontur.Tracing.Core.Config;

using MoreLinq;

using RemoteQueue.Cassandra.Entities;
using RemoteQueue.Cassandra.Repositories;
using RemoteQueue.Cassandra.Repositories.Indexes;
using RemoteQueue.LocalTasks.TaskQueue;

using log4net;


namespace RemoteQueue.Handling
{
    public class HandlerManager : IHandlerManager
    {
        public HandlerManager(
            ITaskQueue taskQueue,
            ITaskCounter taskCounter,
            IShardingManager shardingManager,
            Func<Tuple<string, ColumnInfo>, TaskMetaInformation, long, HandlerTask> createHandlerTask,
            TaskHandlerCollection taskHandlerCollection,
            IHandleTasksMetaStorage handleTasksMetaStorage)
        {
            this.taskQueue = taskQueue;
            this.taskCounter = taskCounter;
            this.shardingManager = shardingManager;
            this.createHandlerTask = createHandlerTask;
            this.taskHandlerCollection = taskHandlerCollection;
            this.handleTasksMetaStorage = handleTasksMetaStorage;

            if(!Trace.IsInitialized)
                Trace.Initialize(new StaticConfigurationProvider(new TracingConfig(true, "edi-test", "http://vm-elastic:9003/spans")));
        }

        public void Run()
        {
            lock(lockObject)
            {
                var nowTicks = DateTime.UtcNow.Ticks;
                var taskInfoBatches = handleTasksMetaStorage
                    .GetAllTasksInStates(nowTicks, TaskState.New, TaskState.WaitingForRerun, TaskState.InProcess, TaskState.WaitingForRerunAfterError)
                    .Batch(100, Enumerable.ToArray);
                if(logger.IsDebugEnabled)
                    logger.DebugFormat("Начали обработку очереди.");
                foreach(var taskInfoBatch in taskInfoBatches)
                {
                    var metas = handleTasksMetaStorage.GetMetasQuiet(taskInfoBatch.Select(x => x.Item1).ToArray());
                    for(var i = 0; i < taskInfoBatch.Length; i++)
                    {
                        var meta = metas[i];
                        var taskInfo = taskInfoBatch[i];
                        if(!taskCounter.CanQueueTask(TaskQueueReason.PullFromQueue)) return;

                        ITraceContext taskTraceContext = null;
                        if(meta != null)
                        {
                            taskTraceContext = Trace.ContinueContext(meta.TraceId, meta.Id, meta.IsActive, true);
                            taskTraceContext.RecordTimepoint(Timepoint.Start, meta.CreationTime);
                        }

                        QueueTask(taskInfo, meta, nowTicks, TaskQueueReason.PullFromQueue);

                        if(taskTraceContext != null)
                        {
                            taskTraceContext.RecordTimepoint(Timepoint.Finish);
                            Trace.FinishCurrentContext();
                        }
                    }
                }
            }
        }

        public string Id { get { return "HandlerManager"; } }

        public void Start()
        {
            taskQueue.Start();
        }

        public void Stop()
        {
            if(!started)
                return;
            started = false;
            taskQueue.StopAndWait(100 * 1000);
        }

        public long GetQueueLength()
        {
            return taskQueue.GetQueueLength();
        }

        public Tuple<long, long> GetCassandraQueueLength()
        {
            var allTasksInStates = handleTasksMetaStorage.GetAllTasksInStates(DateTime.UtcNow.Ticks, TaskState.New, TaskState.WaitingForRerun, TaskState.InProcess, TaskState.WaitingForRerunAfterError);
            long all = 0;
            long forMe = 0;
            foreach(var allTasksInState in allTasksInStates)
            {
                all++;
                if(shardingManager.IsSituableTask(allTasksInState.Item1))
                    forMe++;
            }
            return new Tuple<long, long>(all, forMe);
        }

        internal void QueueTask(Tuple<string, ColumnInfo> taskInfo, TaskMetaInformation meta, long nowTicks, TaskQueueReason reason)
        {
            if(meta != null && !taskHandlerCollection.ContainsHandlerFor(meta.Name))
                return;
            if(!shardingManager.IsSituableTask(taskInfo.Item1))
                return;

            var handlerTask = createHandlerTask(taskInfo, meta, nowTicks);
            handlerTask.Reason = reason;

            ITraceContext handlerTraceContext = null;
            if(meta != null && reason==TaskQueueReason.PullFromQueue)
            {
                handlerTraceContext = Trace.CreateChildContext("Handler");
                handlerTraceContext.RecordTimepoint(Timepoint.Start);
            }

            taskQueue.QueueTask(handlerTask);

            if(handlerTraceContext!=null)
                Trace.FinishCurrentContext();
        }

        private static readonly ILog logger = LogManager.GetLogger(typeof(HandlerManager));

        private readonly Func<Tuple<string, ColumnInfo>, TaskMetaInformation, long, HandlerTask> createHandlerTask;
        private readonly TaskHandlerCollection taskHandlerCollection;
        private readonly IHandleTasksMetaStorage handleTasksMetaStorage;
        private readonly object lockObject = new object();
        private readonly ITaskQueue taskQueue;
        private readonly ITaskCounter taskCounter;
        private readonly IShardingManager shardingManager;
        private volatile bool started;
    }
}