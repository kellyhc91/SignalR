﻿// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved. See License.md in the project root for license information.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace Microsoft.AspNet.SignalR.Infrastructure
{
    /// <summary>
    /// Manages performance counters using Windows performance counters.
    /// </summary>
    public class PerformanceCounterManager : IPerformanceCounterManager
    {
        /// <summary>
        /// The performance counter category name for SignalR counters.
        /// </summary>
        public const string CategoryName = "SignalR";

        private readonly static PropertyInfo[] _counterProperties = GetCounterPropertyInfo();
        private readonly static IPerformanceCounter _noOpCounter = new NoOpPerformanceCounter();
        private volatile bool _initialized;
        private object _initLocker = new object();

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        public PerformanceCounterManager()
        {
            InitNoOpCounters();
        }

        /// <summary>
        /// Gets the performance counter representing the total number of connection Connect events since the application was started.
        /// </summary>
        [PerformanceCounter(Name = "Connections Connected", Description = "The total number of connection Connect events since the application was started.", CounterType = PerformanceCounterType.NumberOfItems32)]
        public IPerformanceCounter ConnectionsConnected { get; private set; }

        /// <summary>
        /// Gets the performance counter representing the total number of connection Reconnect events since the application was started.
        /// </summary>
        [PerformanceCounter(Name = "Connections Reconnected", Description = "The total number of connection Reconnect events since the application was started.", CounterType = PerformanceCounterType.NumberOfItems32)]
        public IPerformanceCounter ConnectionsReconnected { get; private set; }

        /// <summary>
        /// Gets the performance counter representing the total number of connection Disconnect events since the application was started.
        /// </summary>
        [PerformanceCounter(Name = "Connections Disconnected", Description = "The total number of connection Disconnect events since the application was started.", CounterType = PerformanceCounterType.NumberOfItems32)]
        public IPerformanceCounter ConnectionsDisconnected { get; private set; }

        /// <summary>
        /// Gets the performance counter representing the number of connections currently connected.
        /// </summary>
        [PerformanceCounter(Name = "Connections Current", Description = "The number of connections currently connected.", CounterType = PerformanceCounterType.NumberOfItems32)]
        public IPerformanceCounter ConnectionsCurrent { get; private set; }

        /// <summary>
        /// Gets the performance counter representing the toal number of messages received by connections (server to client) since the application was started.
        /// </summary>
        [PerformanceCounter(Name = "Connection Messages Received Total", Description = "The toal number of messages received by connections (server to client) since the application was started.", CounterType = PerformanceCounterType.NumberOfItems64)]
        public IPerformanceCounter ConnectionMessagesReceivedTotal { get; private set; }

        /// <summary>
        /// Gets the performance counter representing the total number of messages sent by connections (client to server) since the application was started.
        /// </summary>
        [PerformanceCounter(Name = "Connection Messages Sent Total", Description = "The total number of messages sent by connections (client to server) since the application was started.", CounterType = PerformanceCounterType.NumberOfItems64)]
        public IPerformanceCounter ConnectionMessagesSentTotal { get; private set; }

        /// <summary>
        /// Gets the performance counter representing the number of messages received by connections (server to client) per second.
        /// </summary>
        [PerformanceCounter(Name = "Connection Messages Received/Sec", Description = "The number of messages received by connections (server to client) per second.", CounterType = PerformanceCounterType.RateOfCountsPerSecond32)]
        public IPerformanceCounter ConnectionMessagesReceivedPerSec { get; private set; }

        /// <summary>
        /// Gets the performance counter representing the number of messages sent by connections (client to server) per second.
        /// </summary>
        [PerformanceCounter(Name = "Connection Messages Sent/Sec", Description = "The number of messages sent by connections (client to server) per second.", CounterType = PerformanceCounterType.RateOfCountsPerSecond32)]
        public IPerformanceCounter ConnectionMessagesSentPerSec { get; private set; }

        /// <summary>
        /// Gets the performance counter representing the total number of messages published to the message bus since the application was started.
        /// </summary>
        [PerformanceCounter(Name = "Messages Bus Messages Published Total", Description = "The total number of messages published to the message bus since the application was started.", CounterType = PerformanceCounterType.NumberOfItems64)]
        public IPerformanceCounter MessageBusMessagesPublishedTotal { get; private set; }

        /// <summary>
        /// Gets the performance counter representing the number of messages published to the message bus per second.
        /// </summary>
        [PerformanceCounter(Name = "Messages Bus Messages Published/Sec", Description = "The number of messages published to the message bus per second.", CounterType = PerformanceCounterType.RateOfCountsPerSecond32)]
        public IPerformanceCounter MessageBusMessagesPublishedPerSec { get; private set; }

        /// <summary>
        /// Gets the performance counter representing the current number of subscribers to the message bus.
        /// </summary>
        [PerformanceCounter(Name = "Message Bus Subscribers Current", Description = "The current number of subscribers to the message bus.", CounterType = PerformanceCounterType.NumberOfItems32)]
        public IPerformanceCounter MessageBusSubscribersCurrent { get; private set; }

        /// <summary>
        /// Gets the performance counter representing the total number of subscribers to the message bus since the application was started.
        /// </summary>
        [PerformanceCounter(Name = "Message Bus Subscribers Total", Description = "The total number of subscribers to the message bus since the application was started.", CounterType = PerformanceCounterType.NumberOfItems32)]
        public IPerformanceCounter MessageBusSubscribersTotal { get; private set; }

        /// <summary>
        /// Gets the performance counter representing the number of new subscribers to the message bus per second.
        /// </summary>
        [PerformanceCounter(Name = "Message Bus Subscribers/Sec", Description = "The number of new subscribers to the message bus per second.", CounterType = PerformanceCounterType.RateOfCountsPerSecond32)]
        public IPerformanceCounter MessageBusSubscribersPerSec { get; private set; }

        /// <summary>
        /// Gets the performance counter representing the number of workers allocated to deliver messages in the message bus.
        /// </summary>
        [PerformanceCounter(Name = "Message Bus Allocated Workers", Description = "The number of workers allocated to deliver messages in the message bus.", CounterType = PerformanceCounterType.NumberOfItems32)]
        public IPerformanceCounter MessageBusAllocatedWorkers { get; private set; }

        /// <summary>
        /// Gets the performance counter representing the number of workers currently busy delivering messages in the message bus.
        /// </summary>
        [PerformanceCounter(Name = "Message Bus Busy Workers", Description = "The number of workers currently busy delivering messages in the message bus.", CounterType = PerformanceCounterType.NumberOfItems32)]
        public IPerformanceCounter MessageBusBusyWorkers { get; private set; }

        /// <summary>
        /// Gets the performance counter representing the total number of all errors processed since the application was started.
        /// </summary>
        [PerformanceCounter(Name = "Errors: All Total", Description = "The total number of all errors processed since the application was started.", CounterType = PerformanceCounterType.NumberOfItems32)]
        public IPerformanceCounter ErrorsAllTotal { get; private set; }

        /// <summary>
        /// Gets the performance counter representing the number of all errors processed per second.
        /// </summary>
        [PerformanceCounter(Name = "Errors: All/Sec", Description = "The number of all errors processed per second.", CounterType = PerformanceCounterType.RateOfCountsPerSecond32)]
        public IPerformanceCounter ErrorsAllPerSec { get; private set; }

        /// <summary>
        /// Gets the performance counter representing the total number of hub resolution errors processed since the application was started.
        /// </summary>
        [PerformanceCounter(Name = "Errors: Hub Resolution Total", Description = "The total number of hub resolution errors processed since the application was started.", CounterType = PerformanceCounterType.NumberOfItems32)]
        public IPerformanceCounter ErrorsHubResolutionTotal { get; private set; }

        /// <summary>
        /// Gets the performance counter representing the number of hub resolution errors per second.
        /// </summary>
        [PerformanceCounter(Name = "Errors: Hub Resolution/Sec", Description = "The number of hub resolution errors per second.", CounterType = PerformanceCounterType.RateOfCountsPerSecond32)]
        public IPerformanceCounter ErrorsHubResolutionPerSec { get; private set; }

        /// <summary>
        /// Gets the performance counter representing the total number of hub invocation errors processed since the application was started.
        /// </summary>
        [PerformanceCounter(Name = "Errors: Hub Invocation Total", Description = "The total number of hub invocation errors processed since the application was started.", CounterType = PerformanceCounterType.NumberOfItems32)]
        public IPerformanceCounter ErrorsHubInvocationTotal { get; private set; }

        /// <summary>
        /// Gets the performance counter representing the number of hub invocation errors per second.
        /// </summary>
        [PerformanceCounter(Name = "Errors: Hub Invocation/Sec", Description = "The number of hub invocation errors per second.", CounterType = PerformanceCounterType.RateOfCountsPerSecond32)]
        public IPerformanceCounter ErrorsHubInvocationPerSec { get; private set; }

        /// <summary>
        /// Gets the performance counter representing the total number of transport errors processed since the application was started.
        /// </summary>
        [PerformanceCounter(Name = "Errors: Tranport Total", Description = "The total number of transport errors processed since the application was started.", CounterType = PerformanceCounterType.NumberOfItems32)]
        public IPerformanceCounter ErrorsTransportTotal { get; private set; }

        /// <summary>
        /// Gets the performance counter representing the number of transport errors per second.
        /// </summary>
        [PerformanceCounter(Name = "Errors: Transport/Sec", Description = "The number of transport errors per second.", CounterType = PerformanceCounterType.RateOfCountsPerSecond32)]
        public IPerformanceCounter ErrorsTransportPerSec { get; private set; }

        /// <summary>
        /// Initializes the performance counters.
        /// </summary>
        /// <param name="instanceName">The host instance name.</param>
        /// <param name="hostShutdownToken">The CancellationToken representing the host shutdown.</param>
        public void Initialize(string instanceName, CancellationToken hostShutdownToken)
        {
            if (_initialized)
            {
                return;
            }

            var needToRegisterWithShutdownToken = false;
            lock (_initLocker)
            {
                if (!_initialized)
                {
                    instanceName = instanceName ?? Guid.NewGuid().ToString();
                    SetCounterProperties(instanceName);
                    // The initializer ran, so let's register the shutdown cleanup
                    if (hostShutdownToken != null)
                    {
                        needToRegisterWithShutdownToken = true;
                    }
                    _initialized = true;
                }
            }

            if (needToRegisterWithShutdownToken)
            {
                hostShutdownToken.Register(UnloadCounters);
            }
        }

        private void UnloadCounters()
        {
            lock (_initLocker)
            {
                if (!_initialized)
                {
                    // We were never initalized
                    return;
                }
            }

            var counterProperties = this.GetType()
                .GetProperties()
                .Where(p => p.PropertyType == typeof(IPerformanceCounter));

            foreach (var property in counterProperties)
            {
                var counter = property.GetValue(this, null) as IPerformanceCounter;
                counter.Close();
                counter.RemoveInstance();
            }
        }

        private void InitNoOpCounters()
        {
            // Set all the counter properties to no-op by default.
            // These will get reset to real counters when/if the Initialize method is called.
            foreach (var property in _counterProperties)
            {
                property.SetValue(this, new NoOpPerformanceCounter(), null);
            }
        }

        private void SetCounterProperties(string instanceName)
        {
            bool loadCounters = true;
            foreach (var property in _counterProperties)
            {
                PerformanceCounterAttribute attribute = GetPerformanceCounterAttribute(property);

                if (attribute == null)
                {
                    continue;
                }

                IPerformanceCounter counter = null;

                if (loadCounters)
                {
                    counter = LoadCounter(CategoryName, attribute.Name, instanceName);

                    if (counter == null)
                    {
                        loadCounters = false;
                    }
                }

                counter = counter ?? _noOpCounter;

                // Initialize the counter sample
                counter.NextSample(); 

                property.SetValue(this, counter, null);
            }
        }

        internal static PropertyInfo[] GetCounterPropertyInfo()
        {
            return typeof(PerformanceCounterManager)
                .GetProperties()
                .Where(p => p.PropertyType == typeof(IPerformanceCounter))
                .ToArray();
        }

        internal static PerformanceCounterAttribute GetPerformanceCounterAttribute(PropertyInfo property)
        {
            return property.GetCustomAttributes(typeof(PerformanceCounterAttribute), false)
                    .Cast<PerformanceCounterAttribute>()
                    .SingleOrDefault();
        }

        [SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope", Justification = "Counters are disposed later")]
        private static IPerformanceCounter LoadCounter(string categoryName, string counterName, string instanceName)
        {
            try
            {
                if (PerformanceCounterCategory.Exists(categoryName) && PerformanceCounterCategory.CounterExists(counterName, categoryName))
                {
                    return new PerformanceCounterWrapper(new PerformanceCounter(categoryName, counterName, instanceName, readOnly: false));
                }
                return null;
            }
            catch (InvalidOperationException) { return null; }
            catch (UnauthorizedAccessException) { return null; }
        }
    }
}
