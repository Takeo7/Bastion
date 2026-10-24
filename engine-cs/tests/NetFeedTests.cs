using System.Collections.Generic;
using System.Linq;
using Bastion.Net;
using Xunit;

namespace Bastion.Engine.Tests
{
    /// <summary>The host's messages on their way to the game (Net/ServerFeed.cs): the gaps, and what waits while nobody listens.</summary>
    public class NetFeedTests
    {
        private static SnapshotMessage Snapshot(int seq) => new SnapshotMessage { State = new GameState { Seq = seq } };

        private static EventsMessage Events(int seq, int count) => new EventsMessage { Events = Enumerable.Range(0, count).Select((_) => (GameEvent)new ReadyEvent()).ToList(), Seq = seq };

        [Fact]
        public void EventsInStepGoThrough()
        {
            var feed = new ServerFeed();
            var heard = new List<string>();
            var resyncs = new List<string>();
            feed.Message += (m) => heard.Add(m.T);
            feed.Resync += resyncs.Add;
            feed.Take(Snapshot(10));
            feed.Take(Events(12, 2));
            feed.Take(Events(13, 1));
            Assert.Equal(new[] { "snapshot", "events", "events" }, heard);
            Assert.Empty(resyncs);
        }

        [Fact]
        public void AGapAsksForTheStateAndDropsEventsUntilItComes()
        {
            var feed = new ServerFeed();
            var heard = new List<int>();
            var resyncs = new List<string>();
            feed.Message += (m) => heard.Add(m is EventsMessage e ? e.Seq : m is SnapshotMessage s ? -s.State!.Seq : 0);
            feed.Resync += resyncs.Add;
            feed.Take(Snapshot(10));
            // 11 and 12 lost: 13 comes after them.
            feed.Take(Events(13, 1));
            Assert.Equal("[sync] desfase de eventos (11 ≠ 13): se pide el estado entero", resyncs.Single());
            feed.Take(Events(14, 1));
            feed.Take(Snapshot(14));
            feed.Take(Events(15, 1));
            Assert.Equal(new[] { -10, -14, 15 }, heard);
            Assert.Single(resyncs);
        }

        [Fact]
        public void WhatComesWithNobodyListeningWaitsForTheFirstWhoDoes()
        {
            var feed = new ServerFeed();
            feed.Take(new WelcomeMessage { Slot = 1 });
            feed.Take(new RoomMessage { Phase = "lobby" });
            feed.Take(Snapshot(3));
            feed.Take(new RoomMessage { Phase = "mission" });
            // A new mission: the one before is moot.
            feed.Take(Snapshot(0));
            feed.Take(Events(1, 1));
            Assert.Equal("mission", feed.LastRoom!.Phase);
            var heard = new List<string>();
            feed.Message += (m) => heard.Add(m is SnapshotMessage s ? $"snapshot {s.State!.Seq}" : m.T);
            Assert.Equal(new[] { "welcome", "room", "room", "snapshot 0", "events" }, heard);
            feed.Take(Events(2, 1));
            Assert.Equal("events", heard.Last());
            Assert.Equal(6, heard.Count);
        }

        [Fact]
        public void TooMuchWaitingAsksForTheStateAgain()
        {
            var feed = new ServerFeed();
            var resyncs = new List<string>();
            feed.Resync += resyncs.Add;
            feed.Take(Snapshot(0));
            for (int i = 1; i <= ServerFeed.MaxHeld; i++) feed.Take(Events(i, 1));
            Assert.Single(resyncs);
            feed.Take(Snapshot(ServerFeed.MaxHeld));
            var heard = new List<string>();
            feed.Message += (m) => heard.Add(m.T);
            Assert.Equal(new[] { "snapshot" }, heard);
        }
    }
}
