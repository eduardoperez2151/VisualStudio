﻿using System;
using System.Linq;
using System.Threading;
using System.ComponentModel;
using System.Collections.Generic;
using GitHub.Services;
using GitHub.VisualStudio;
using GitHub.VisualStudio.Base;
using NUnit.Framework;
using NSubstitute;
using Microsoft.VisualStudio.TeamFoundation.Git.Extensibility;

public class VSGitExtTests
{
    public class TheConstructor
    {
        [TestCase(true, 1)]
        [TestCase(false, 0)]
        public void GetServiceIGitExt_WhenSccProviderContextIsActive(bool isActive, int expectCalls)
        {
            var context = CreateVSUIContext(isActive);
            var sp = Substitute.For<IGitHubServiceProvider>();

            var target = CreateVSGitExt(context, sp: sp);

            sp.Received(expectCalls).GetService<IGitExt>();
        }

        [TestCase(true, 1)]
        [TestCase(false, 0)]
        public void GetServiceIGitExt_WhenUIContextChanged(bool activated, int expectCalls)
        {
            var context = CreateVSUIContext(false);
            var sp = Substitute.For<IGitHubServiceProvider>();
            var target = CreateVSGitExt(context, sp: sp);

            var eventArgs = new VSUIContextChangedEventArgs(activated);
            context.UIContextChanged += Raise.Event<EventHandler<VSUIContextChangedEventArgs>>(context, eventArgs);

            sp.Received(expectCalls).GetService<IGitExt>();
        }

        [Test]
        public void ActiveRepositories_ReadUsingThreadPoolThread()
        {
            var gitExt = Substitute.For<IGitExt>();
            bool threadPool = false;
            gitExt.ActiveRepositories.Returns(x =>
            {
                threadPool = Thread.CurrentThread.IsThreadPoolThread;
                return new IGitRepositoryInfo[0];
            });

            var target = CreateVSGitExt(gitExt: gitExt);

            Assert.That(threadPool, Is.True);
        }
    }

    public class TheActiveRepositoriesChangedEvent
    {
        [Test]
        public void GitExtPropertyChangedEvent_ActiveRepositoriesChangedIsFired()
        {
            var context = CreateVSUIContext(true);
            var gitExt = CreateGitExt();

            var target = CreateVSGitExt(context, gitExt);

            bool wasFired = false;
            target.ActiveRepositoriesChanged += () => wasFired = true;
            var eventArgs = new PropertyChangedEventArgs(nameof(gitExt.ActiveRepositories));
            gitExt.PropertyChanged += Raise.Event<PropertyChangedEventHandler>(gitExt, eventArgs);

            Assert.That(wasFired, Is.True);
        }

        [Test]
        public void WhenUIContextChanged_ActiveRepositoriesChangedIsFired()
        {
            var context = CreateVSUIContext(false);
            var gitExt = CreateGitExt();
            var target = CreateVSGitExt(context, gitExt);

            bool wasFired = false;
            target.ActiveRepositoriesChanged += () => wasFired = true;

            var eventArgs = new VSUIContextChangedEventArgs(true);
            context.UIContextChanged += Raise.Event<EventHandler<VSUIContextChangedEventArgs>>(context, eventArgs);

            Assert.That(wasFired, Is.True);
        }
    }

    public class TheActiveRepositoriesProperty
    {
        [Test]
        public void SccProviderContextNotActive_IsEmpty()
        {
            var context = CreateVSUIContext(false);
            var target = CreateVSGitExt(context);

            Assert.That(target.ActiveRepositories, Is.Empty);
        }

        [Test]
        public void SccProviderContextIsActive_DefaultEmptyListFromGitExt()
        {
            var context = CreateVSUIContext(true);
            var gitExt = CreateGitExt(new string[0]);
            var target = CreateVSGitExt(context, gitExt);

            var activeRepositories = target.ActiveRepositories;

            Assert.That(activeRepositories.Count(), Is.EqualTo(0));
        }

        // TODO: We can't currently test returning a non-empty list because it constructs a live LocalRepositoryModel object.
        // We could move the responsibility for constructing a LocalRepositoryModel object outside of VSGitExt.
    }

    static IReadOnlyList<IGitRepositoryInfo> CreateActiveRepositories(IList<string> repositoryPaths)
    {
        var repositories = new List<IGitRepositoryInfo>();
        foreach (var repositoryPath in repositoryPaths)
        {
            var repoInfo = Substitute.For<IGitRepositoryInfo>();
            repoInfo.RepositoryPath.Returns(repositoryPath);
            repositories.Add(repoInfo);
        }

        return repositories.AsReadOnly();
    }

    static IVSGitExt CreateVSGitExt(IVSUIContext context = null, IGitExt gitExt = null, IGitHubServiceProvider sp = null)
    {
        context = context ?? CreateVSUIContext(true);
        gitExt = gitExt ?? CreateGitExt();
        sp = sp ?? Substitute.For<IGitHubServiceProvider>();
        var factory = Substitute.For<IVSUIContextFactory>();
        var contextGuid = new Guid(Guids.GitSccProviderId);
        factory.GetUIContext(contextGuid).Returns(context);
        sp.GetService<IVSUIContextFactory>().Returns(factory);
        sp.GetService<IGitExt>().Returns(gitExt);
        var vsGitExt = new VSGitExt(sp, factory);
        vsGitExt.InitializeTask.Wait();
        return vsGitExt;
    }

    static IGitExt CreateGitExt(IList<string> repositoryPaths = null)
    {
        repositoryPaths = repositoryPaths ?? new string[0];
        var gitExt = Substitute.For<IGitExt>();
        var repoList = CreateActiveRepositories(new string[0]);
        gitExt.ActiveRepositories.Returns(repoList);
        return gitExt;
    }

    static IVSUIContext CreateVSUIContext(bool isActive)
    {
        var context = Substitute.For<IVSUIContext>();
        context.IsActive.Returns(isActive);
        return context;
    }
}
