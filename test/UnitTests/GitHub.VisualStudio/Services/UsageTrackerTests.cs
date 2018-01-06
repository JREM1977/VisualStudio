﻿using System;
using System.Collections.ObjectModel;
using System.Reactive.Disposables;
using System.Threading.Tasks;
using GitHub.Extensions;
using GitHub.Models;
using GitHub.Services;
using GitHub.Settings;
using NSubstitute;
using NUnit.Framework;


namespace UnitTests.GitHub.VisualStudio.Services
{
    public class UsageTrackerTests
    {
        public class TheTimer : TestBaseClass
        {
            [Test]
            public void ShouldStartTimer()
            {
                var service = Substitute.For<IUsageService>();
                var target = new UsageTracker(CreateServiceProvider(), service);

                service.Received(1).StartTimer(Arg.Any<Func<Task>>(), TimeSpan.FromMinutes(3), TimeSpan.FromHours(8));
            }

            [Test]
            public async Task FirstTickShouldIncrementLaunchCount()
            {
                var service = CreateUsageService();
                var targetAndTick = CreateTargetAndGetTick(CreateServiceProvider(), service);

                await targetAndTick.Item2();

                await service.Received(1).WriteLocalData(
                    Arg.Is<UsageData>(x => 
                        x.Model.NumberOfStartups == 1 &&
                        x.Model.NumberOfStartupsWeek == 1 &&
                        x.Model.NumberOfStartupsMonth == 1));
            }

            [Test]
            public async Task SubsequentTickShouldNotIncrementLaunchCount()
            {
                var service = CreateUsageService();
                var targetAndTick = CreateTargetAndGetTick(CreateServiceProvider(), service);

                await targetAndTick.Item2();
                service.ClearReceivedCalls();
                await targetAndTick.Item2();

                await service.DidNotReceiveWithAnyArgs().WriteLocalData(null);
            }

            [Test]
            public async Task ShouldDisposeTimerIfMetricsServiceNotFound()
            {
                var service = CreateUsageService();
                var disposed = false;
                var disposable = Disposable.Create(() => disposed = true);
                service.StartTimer(null, new TimeSpan(), new TimeSpan()).ReturnsForAnyArgs(disposable);

                var targetAndTick = CreateTargetAndGetTick(
                    CreateServiceProvider(hasMetricsService: false),
                    service);

                await targetAndTick.Item2();

                Assert.True(disposed);
            }

            [Test]
            public async Task TickShouldNotSendDataIfSameDay()
            {
                var serviceProvider = CreateServiceProvider();
                var targetAndTick = CreateTargetAndGetTick(
                    serviceProvider,
                    CreateUsageService());

                await targetAndTick.Item2();

                var metricsService = serviceProvider.TryGetService<IMetricsService>();
                await metricsService.DidNotReceive().PostUsage(Arg.Any<UsageModel>());
            }

            [Test]
            public async Task TickShouldSendDataIfDifferentDay()
            {
                var serviceProvider = CreateServiceProvider();
                var targetAndTick = CreateTargetAndGetTick(
                    serviceProvider,
                    CreateUsageService(sameDay: false));

                await targetAndTick.Item2();

                var metricsService = serviceProvider.TryGetService<IMetricsService>();
                await metricsService.Received(1).PostUsage(Arg.Any<UsageModel>());
            }

            [Test]
            public async Task NonWeeklyOrMonthlyCountersShouldBeZeroed()
            {
                var service = CreateUsageService(new UsageModel
                {
                    NumberOfStartups = 1,
                    NumberOfStartupsWeek = 1,
                    NumberOfStartupsMonth = 1,
                    NumberOfClones = 1,
                }, sameDay: false);
                Func<Task> tick = null;

                service.WhenForAnyArgs(x => x.StartTimer(null, new TimeSpan(), new TimeSpan()))
                    .Do(x => tick = x.ArgAt<Func<Task>>(0));

                var serviceProvider = CreateServiceProvider();
                var target = new UsageTracker(serviceProvider, service);

                await tick();

                await service.Received().WriteLocalData(
                    Arg.Is<UsageData>(x =>
                        x.Model.NumberOfStartups == 0 &&
                        x.Model.NumberOfStartupsWeek == 2 &&
                        x.Model.NumberOfStartupsMonth == 2 &&
                        x.Model.NumberOfClones == 0));
            }

            [Test]
            public async Task NonMonthlyCountersShouldBeZeroed()
            {
                var service = CreateUsageService(new UsageModel
                {
                    NumberOfStartups = 1,
                    NumberOfStartupsWeek = 1,
                    NumberOfStartupsMonth = 1,
                    NumberOfClones = 1,
                }, sameDay: false, sameWeek: false);
                Func<Task> tick = null;

                service.WhenForAnyArgs(x => x.StartTimer(null, new TimeSpan(), new TimeSpan()))
                    .Do(x => tick = x.ArgAt<Func<Task>>(0));

                var serviceProvider = CreateServiceProvider();
                var target = new UsageTracker(serviceProvider, service);

                await tick();

                await service.Received().WriteLocalData(
                    Arg.Is<UsageData>(x =>
                        x.Model.NumberOfStartups == 0 &&
                        x.Model.NumberOfStartupsWeek == 0 &&
                        x.Model.NumberOfStartupsMonth == 2 &&
                        x.Model.NumberOfClones == 0));
            }

            [Test]
            public async Task AllCountersShouldBeZeroed()
            {
                var service = CreateUsageService(new UsageModel
                {
                    NumberOfStartups = 1,
                    NumberOfStartupsWeek = 1,
                    NumberOfStartupsMonth = 1,
                    NumberOfClones = 1,
                }, sameDay: false, sameWeek: false, sameMonth: false);
                Func<Task> tick = null;

                service.WhenForAnyArgs(x => x.StartTimer(null, new TimeSpan(), new TimeSpan()))
                    .Do(x => tick = x.ArgAt<Func<Task>>(0));

                var serviceProvider = CreateServiceProvider();
                var target = new UsageTracker(serviceProvider, service);

                await tick();

                await service.Received().WriteLocalData(
                    Arg.Is<UsageData>(x =>
                        x.Model.NumberOfStartups == 0 &&
                        x.Model.NumberOfStartupsWeek == 0 &&
                        x.Model.NumberOfStartupsMonth == 0 &&
                        x.Model.NumberOfClones == 0));
            }
        }

        public class TheIncrementCounterMethod : TestBaseClass
        {
            [Test]
            public async Task ShouldIncrementCounter()
            {
                var model = new UsageModel { NumberOfClones = 4 };
                var target = new UsageTracker(
                    CreateServiceProvider(),
                    CreateUsageService(model));

                await target.IncrementCounter(x => x.NumberOfClones);

                Assert.AreEqual(5, model.NumberOfClones);
            }

            [Test]
            public async Task ShouldWriteUpdatedData()
            {
                var data = new UsageData { Model = new UsageModel() };
                var service = CreateUsageService(data);
                var target = new UsageTracker(
                    CreateServiceProvider(),
                    service);

                await target.IncrementCounter(x => x.NumberOfClones);

                await service.Received(1).WriteLocalData(data);
            }
        }

        static Tuple<UsageTracker, Func<Task>> CreateTargetAndGetTick(
            IGitHubServiceProvider serviceProvider,
            IUsageService service)
        {
            Func<Task> tick = null;

            service.WhenForAnyArgs(x => x.StartTimer(null, new TimeSpan(), new TimeSpan()))
                .Do(x => tick = x.ArgAt<Func<Task>>(0));

            var target = new UsageTracker(serviceProvider, service);

            return Tuple.Create(target, tick);
        }

        static IGitHubServiceProvider CreateServiceProvider(bool hasMetricsService = true)
        {
            var result = Substitute.For<IGitHubServiceProvider>();
            var connectionManager = Substitute.For<IConnectionManager>();
            var metricsService = Substitute.For<IMetricsService>();
            var packageSettings = Substitute.For<IPackageSettings>();

            connectionManager.Connections.Returns(new ObservableCollectionEx<IConnection>());
            packageSettings.CollectMetrics.Returns(true);

            result.GetService<IConnectionManager>().Returns(connectionManager);
            result.GetService<IPackageSettings>().Returns(packageSettings);
            result.TryGetService<IMetricsService>().Returns(hasMetricsService ? metricsService : null);

            return result;
        }

        static IUsageService CreateUsageService(
            bool sameDay = true,
            bool sameWeek = true,
            bool sameMonth = true)
        {
            return CreateUsageService(new UsageModel(), sameDay, sameWeek, sameMonth);
        }

        static IUsageService CreateUsageService(
            UsageModel model,
            bool sameDay = true,
            bool sameWeek = true,
            bool sameMonth = true)
        {
            return CreateUsageService(new UsageData
            {
                LastUpdated = DateTimeOffset.Now,
                Model = model
            }, sameDay, sameWeek, sameMonth);
        }

        static IUsageService CreateUsageService(
            UsageData data,
            bool sameDay = true,
            bool sameWeek = true,
            bool sameMonth = true)
        {
            var result = Substitute.For<IUsageService>();
            result.ReadLocalData().Returns(data);
            result.IsSameDay(DateTimeOffset.Now).ReturnsForAnyArgs(sameDay);
            result.IsSameWeek(DateTimeOffset.Now).ReturnsForAnyArgs(sameWeek);
            result.IsSameMonth(DateTimeOffset.Now).ReturnsForAnyArgs(sameMonth);
            return result;
        }
    }
}
