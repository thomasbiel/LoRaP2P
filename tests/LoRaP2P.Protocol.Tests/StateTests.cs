using LoRaP2P.Protocol.Frames;
using LoRaP2P.Protocol.Peers;
using LoRaP2P.Protocol.Reliability;
using LoRaP2P.Protocol.Statistics;

namespace LoRaP2P.Protocol.Tests;

public sealed class StateTests
{
    [Test]
    public async Task ConfigurationRoundTripsAndMissingFileReturnsNull()
    {
        var directory = Path.Combine(TestContext.CurrentContext.WorkDirectory, Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "p2p.json");
        var store = new P2pConfigurationStore(path);
        Assert.That(await store.LoadAsync(), Is.Null);

        var configuration = new P2pConfiguration
        {
            LocalNodeId = new P2pNodeId(1),
            Peers = [new P2pNodeId(2)],
            Options = new P2pOptions { MaxRetries = 4 }
        };

        try
        {
            await store.SaveAsync(configuration);
            var loaded = await store.LoadAsync();

            Assert.Multiple(() =>
            {
                Assert.That(loaded!.LocalNodeId, Is.EqualTo(configuration.LocalNodeId));
                Assert.That(loaded.Peers, Is.EqualTo(configuration.Peers));
                Assert.That(loaded.Options.MaxRetries, Is.EqualTo(4));
                Assert.That(Directory.GetFiles(directory, "*.tmp"), Is.Empty);
            });
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Test]
    public async Task ConfigurationRejectsInvalidJsonAndSemantics()
    {
        var directory = Path.Combine(TestContext.CurrentContext.WorkDirectory, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "p2p.json");
        var store = new P2pConfigurationStore(path);

        try
        {
            await File.WriteAllTextAsync(path, "{not-json");
            Assert.That(async () => await store.LoadAsync(), Throws.TypeOf<InvalidDataException>());

            await File.WriteAllTextAsync(
                path,
                """{"LocalNodeId":1,"Peers":[1],"AckTimeout":"00:00:02","MaxRetries":3,"BackoffMinimum":"00:00:00.250","BackoffMaximum":"00:00:01","AckTurnaroundDelay":"00:00:00.100","DuplicateWindow":"00:10:00","DuplicateEntriesPerPeer":64}""");
            Assert.That(async () => await store.LoadAsync(), Throws.TypeOf<InvalidDataException>());
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public void PeerRegistryTracksConfiguredAndObservedPeers()
    {
        var registry = new PeerRegistry([new P2pNodeId(2)]);
        registry.Add(new P2pNodeId(3));
        registry.RecordReception(new P2pNodeId(2), DateTimeOffset.UnixEpoch, -70, 4.5, 99);

        var peers = registry.GetSnapshot();
        Assert.Multiple(() =>
        {
            Assert.That(peers.Select(static peer => peer.NodeId.Value), Is.EqualTo(new ushort[] { 2, 3 }));
            Assert.That(peers[0].LastRssiDbm, Is.EqualTo(-70));
            Assert.That(peers[0].LastSequenceNumber, Is.EqualTo(99));
            Assert.That(registry.Add(new P2pNodeId(2)), Is.False);
        });
    }

    [Test]
    public void DuplicateTrackerExpiresAndEvictsOldestEntries()
    {
        var tracker = new DuplicateTracker(TimeSpan.FromMinutes(10), 2);
        var sender = new P2pNodeId(2);
        var now = DateTimeOffset.UnixEpoch;

        Assert.Multiple(() =>
        {
            Assert.That(tracker.IsDuplicateAndTrack(sender, 1, now), Is.False);
            Assert.That(tracker.IsDuplicateAndTrack(sender, 1, now.AddMinutes(1)), Is.True);
            Assert.That(tracker.IsDuplicateAndTrack(sender, 2, now.AddMinutes(2)), Is.False);
            Assert.That(tracker.IsDuplicateAndTrack(sender, 3, now.AddMinutes(3)), Is.False);
            Assert.That(tracker.IsDuplicateAndTrack(sender, 1, now.AddMinutes(4)), Is.False);
            Assert.That(tracker.IsDuplicateAndTrack(sender, 1, now.AddMinutes(15)), Is.False);
        });
    }

    [Test]
    public void SequenceGeneratorWraps()
    {
        var generator = new SequenceNumberGenerator(uint.MaxValue);

        Assert.Multiple(() =>
        {
            Assert.That(generator.Next(), Is.EqualTo(uint.MaxValue));
            Assert.That(generator.Next(), Is.Zero);
        });
    }

    [Test]
    public void StatisticsSnapshotIsThreadSafe()
    {
        var statistics = new P2pStatistics();
        Parallel.For(0, 10_000, _ => statistics.DataFrameSent());

        Assert.That(statistics.GetSnapshot().DataFramesSent, Is.EqualTo(10_000));
    }
}
