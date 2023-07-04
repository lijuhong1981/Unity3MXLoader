using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Timers;
using UnityEngine;

/**
 * Unity3MXLoader
 * 一个加载3MX模型的Unity插件
 * @author lijuhong1981
 */
namespace Unity3MX
{
    public abstract class TaskBase
    {
        enum State
        {
            WAITING,
            RUNNING,
            FINISHED,
            CANCELED,
        }

        private State mState = State.WAITING;
        public bool IsWaiting
        {
            get { return mState == State.WAITING; }
        }
        public bool IsRunning
        {
            get { return mState == State.RUNNING; }
        }
        public bool IsFinished
        {
            get { return mState == State.FINISHED; }
        }
        public bool IsCanceled
        {
            get { return mState == State.CANCELED; }
        }

        //private static uint mNextId = 0;
        //private static uint getNextId()
        //{
        //    return mNextId++;
        //}

        //private uint mId = getNextId();
        //public uint Id
        //{
        //    get { return mId; }
        //}

        public void Cancel()
        {
            mState = State.CANCELED;
        }

        public void Run()
        {
            if (IsCanceled)
                return;
            mState = State.RUNNING;
            ThreadPool.QueueUserWorkItem(o =>
            {
                try
                {
                    runInThread();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("This task error: " + ex);
                }
                mState = State.FINISHED;
            });
        }

        protected abstract void runInThread();

        internal void reset()
        {
            mState = State.WAITING;
        }
    }

    public class TaskPool : IDisposable
    {
        private int mMaxTasks = 16;
        public int MaxTasks
        {
            set => mMaxTasks = value;
            get => mMaxTasks;
        }

        private System.Timers.Timer mTimer = new System.Timers.Timer();
        public bool IsRunning
        {
            get => mTimer.Enabled;
        }
        public double Interval
        {
            set => mTimer.Interval = value;
            get => mTimer.Interval;
        }

        private List<TaskBase> mTaskList = new();
        private List<TaskBase> mWaitingTasks = new();

        public TaskPool(int maxTasks = 16)
        {
            mMaxTasks = maxTasks;
            mTimer.Elapsed += process;
        }

        public void Start()
        {
            if (IsRunning)
                return;
            mTimer.Start();
        }

        public void Stop()
        {
            if (!IsRunning)
                return;
            mTimer.Stop();
        }

        public void Add(TaskBase task)
        {
            if (mTaskList.Contains(task) || !task.IsWaiting)
                return;
            lock (mTaskList)
            {
                mTaskList.Add(task);
            }
        }

        public void Clear()
        {
            lock (mTaskList)
            {
                mTaskList.Clear();
            }
        }

        void process(object sender, ElapsedEventArgs e)
        {
            try
            {
                uint runnningCount = 0;
                mWaitingTasks.Clear();
                lock (mTaskList)
                {
                    if (mTaskList.Count == 0)
                        return;
                    var tasks = mTaskList.ToArray();
                    foreach (var task in tasks)
                    {
                        //移除已完成或已取消的Task
                        if (task.IsFinished || task.IsCanceled)
                            mTaskList.Remove(task);
                        //统计执行中的Task
                        else if (task.IsRunning)
                            runnningCount++;
                        //统计等待中的Task
                        else if (task.IsWaiting)
                            mWaitingTasks.Add(task);
                    }
                }

                int i = 0;
                while (runnningCount < mMaxTasks && i < mWaitingTasks.Count)
                {
                    var task = mWaitingTasks[i];
                    if (task != null)
                    {
                        task.Run();
                        if (task.IsRunning)
                            runnningCount++;
                    }
                    else
                    {
                        //TODO 不知为何，某些情况下task会为null
                        //Debug.LogWarning("This waiting task is null.");
                    }
                    i++;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("This task pool process error: " + ex);
            }
        }

        public void Dispose()
        {
            mTimer.Elapsed -= process;
            mTimer.Dispose();
        }
    }
}
