///////////////////////////////////////////////////////////////////////////////////////
// Copyright (C) 2006-2017 Esper Team. All rights reserved.                           /
// http://esper.codehaus.org                                                          /
// ---------------------------------------------------------------------------------- /
// The software in this package is published under the terms of the GPL license       /
// a copy of which has been included with this distribution in the license.txt file.  /
///////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Threading;

using com.espertech.esper.compat.collections;
using com.espertech.esper.compat.logging;

namespace com.espertech.esper.compat.threading
{
    public class DedicatedExecutorService : IExecutorService
    {
        private readonly Guid _id;
        private readonly int _numThreads;
        private readonly Thread[] _threads;
        private readonly IBlockingQueue<Runnable> _taskQueue;
        private long _tasksRunning;
        private LiveMode _liveMode;
        private long _numExecuted;

        enum LiveMode
        {
            RUN,
            STOPPING,
            STOPPED
        }

        public event ThreadExceptionEventHandler TaskError;

        /// <summary>
        /// Initializes a new instance of the <see cref="DedicatedExecutorService"/> class.
        /// </summary>
        /// <param name="label">The label.</param>
        /// <param name="numThreads">The num threads.</param>
        public DedicatedExecutorService(string label, int numThreads)
            : this(label, numThreads, new LinkedBlockingQueue<Runnable>())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DedicatedExecutorService"/> class.
        /// </summary>
        /// <param name="label">The label.</param>
        /// <param name="numThreads">The num threads.</param>
        /// <param name="taskQueue">The task queue.</param>
        public DedicatedExecutorService(string label, int numThreads, IBlockingQueue<Runnable> taskQueue)
        {
            _id = Guid.NewGuid();
            _numThreads = numThreads;
            _threads = new Thread[numThreads];
            _tasksRunning = 0L;
            _taskQueue = taskQueue;
            _liveMode = LiveMode.RUN;
            _numExecuted = 0L;

            for( int ii = 0 ; ii < _numThreads ; ii++) {
                _threads[ii] = new Thread(HandleTasksInQueue);
                _threads[ii].Name = "DE:" + label + ":" + _id + ":" + ii;
                _threads[ii].IsBackground = true;
                _threads[ii].Start();
            }
        }

        /// <summary>
        /// Gets the number of tasks executed.
        /// </summary>
        /// <value>The number of tasks executed.</value>
        public long NumExecuted
        {
            get { return Interlocked.Read(ref _numExecuted); }
        }

        /// <summary>
        /// Handles the tasks in queue.
        /// </summary>
        private void HandleTasksInQueue()
        {
            bool isDebugEnabled = Log.IsDebugEnabled;

            Log.Debug("HandleTasksInQueue: thread {0} starting with {1}", Thread.CurrentThread.Name, _taskQueue.GetType().Name);

            using (ScopedInstance<IBlockingQueue<Runnable>>.Set(_taskQueue)) // introduces the queue into scope
            {
                while (_liveMode != LiveMode.STOPPED)
                {
                    Runnable task;

                    Interlocked.Increment(ref _tasksRunning);
                    try
                    {
                        if (_taskQueue.Pop(500, out task))
                        {
                            try
                            {
                                task.Invoke();
                            }
                            catch (Exception e)
                            {
                                Log.Warn("HandleTasksInQueue: finished with abnormal termination", e);

                                if (TaskError != null)
                                {
                                    TaskError(this, new ThreadExceptionEventArgs(e));
                                }
                            }
                            finally
                            {
                                Interlocked.Increment(ref _numExecuted);
                            }
                        }
                        else if (_liveMode == LiveMode.STOPPING)
                        {
                            if (isDebugEnabled)
                            {
                                Log.Debug("HandleTasksInQueue: no items detected in queue, terminating");
                            }
                            break;
                        }
                        else if (isDebugEnabled)
                        {
                            Log.Debug("HandleTasksInQueue: no items detected in queue, start loop again");
                        }
                    }
                    finally
                    {
                        Interlocked.Decrement(ref _tasksRunning);
                    }
                }
            }

            Log.Debug("HandleTasksInQueue: thread ending");
        }

        #region Implementation of IExecutorService

        /// <summary>
        /// Submits the specified runnable to the thread pool.
        /// </summary>
        /// <param name="runnable">The runnable.</param>
        /// <returns></returns>
        public Future<Object> Submit(Action runnable)
        {
            var future = new SimpleFutureImpl<Object>();
            _taskQueue.Push(runnable.Invoke);
            return future;
        }

        /// <summary>
        /// Submits the specified callable to the thread pool.
        /// </summary>
        /// <param name="callable">The callable.</param>
        /// <returns></returns>
        public Future<T> Submit<T>(ICallable<T> callable)
        {
            var future = new SimpleFutureImpl<T>();
            _taskQueue.Push(
                () => future.Value = callable.Call());
            return future;
        }

        /// <summary>
        /// Submits the specified callable to the thread pool.
        /// </summary>
        /// <param name="callable">The callable.</param>
        /// <returns></returns>
        public Future<T> Submit<T>(Func<T> callable)
        {
            var future = new SimpleFutureImpl<T>();
            _taskQueue.Push(
                delegate
                {
                    future.Value = callable.Invoke();
                });
            return future;
        }

        /// <summary>
        /// Shutdowns this instance.
        /// </summary>
        public void Shutdown()
        {
            _liveMode = LiveMode.STOPPING;

            Log.Debug(".Shutdown - Marking instance " + _id + " to avoid further queuing");
        }

        /// <summary>
        /// Awaits the termination.
        /// </summary>
        public void AwaitTermination()
        {
            AwaitTermination(new TimeSpan(0, 0, 15));
        }

        /// <summary>
        /// Awaits the termination.
        /// </summary>
        /// <param name="timeout">The timeout.</param>
        public void AwaitTermination(TimeSpan timeout)
        {
            _liveMode = LiveMode.STOPPING;

            long endTime = DateTime.Now.Ticks + ((long) timeout.TotalMilliseconds)*10000;
            long nowTime;

            long taskCount;

            do {
                taskCount = _taskQueue.Count + Interlocked.Read(ref _tasksRunning);
                Log.Debug(".AwaitTermination - Instance {0} waiting for {1} tasks to complete", _id, taskCount);

                if (taskCount == 0)
                    break;

                Thread.Sleep(10);
                nowTime = DateTime.Now.Ticks;
            } while (nowTime < endTime);

            _liveMode = LiveMode.STOPPED ;

            Log.Debug(".AwaitTermination - Instance {0} ending for {1} tasks to complete", _id, taskCount);
        }

        #endregion

        private class SimpleFutureImpl<T> : Future<T>
        {
            private T _value;

            /// <summary>
            /// Initializes a new instance of the <see cref="FutureImpl&lt;T&gt;"/> class.
            /// </summary>
            public SimpleFutureImpl()
            {
                _hasValue = false;
                _value = default(T);
            }

            private bool _hasValue;

            /// <summary>
            /// Gets a value indicating whether this instance has value.
            /// </summary>
            /// <value><c>true</c> if this instance has value; otherwise, <c>false</c>.</value>
            public bool HasValue
            {
                get { return _hasValue; }
            }

            /// <summary>
            /// Gets or sets the value.
            /// </summary>
            /// <value>The value.</value>
            public T Value
            {
                get
                {
                    if (!HasValue)
                    {
                        throw new InvalidOperationException();
                    }

                    return _value;
                }
                set
                {
                    _value = value;
                    _hasValue = true;
                }
            }

            /// <summary>
            /// Gets the value.
            /// </summary>
            /// <param name="timeOut">The time out.</param>
            /// <returns></returns>
            public T GetValue(TimeSpan timeOut)
            {
                throw new NotSupportedException();
            }

            /// <summary>
            /// Gets the result value from the execution.
            /// </summary>
            /// <returns></returns>
            public T GetValueOrDefault()
            {
                if (!_hasValue)
                {
                    return default(T);
                }

                return Value;
            }
        }

        private static readonly ILog Log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
    }
}
