using GameMesh.Aoi;
using GameMesh.Protocol;
using NUnit.Framework;

namespace GameMesh.Tests.EditMode
{
    public sealed class AoiVelocityExtrapolateTests
    {
        [SetUp]
        public void SetUp()
        {
            AoiVelocityClock.Reset();
        }

        [Test]
        public void MoveStartMsZero_DoesNotExtrapolateEvenWhenEnabled()
        {
            Assert.IsFalse(AoiVelocityExtrapolate.ShouldExtrapolate(true, 0, 5f, 0f));
        }

        [Test]
        public void NearZeroSpeed_StopsExtrapolating()
        {
            Assert.IsFalse(AoiVelocityExtrapolate.ShouldExtrapolate(true, 1000, 0.01f, -0.02f));
            Assert.IsTrue(AoiVelocityExtrapolate.ShouldExtrapolate(true, 1000, 5f, 0f));
        }

        [Test]
        public void ConstantVx_TwoSeconds_MatchesPositionPlusVelocityTimesDt()
        {
            // Time base is event server_time_ms, not move_start_ms.
            AoiVelocityExtrapolate.Sample(
                anchorX: 10f, anchorZ: 20f,
                vx: 5f, vz: 0f,
                eventServerTimeMs: 1000,
                estServerNowMs: 3000,
                out var x, out var z);
            Assert.AreEqual(20f, x, 0.001f);
            Assert.AreEqual(20f, z, 0.001f);
        }

        [Test]
        public void UsingMoveStartMsAsEpoch_WouldDoubleCount_DocumentedAgainst()
        {
            // Keyframe at server_time=5000, segment started at move_start=1000, v=5.
            // Correct: dt from server_time → 0 at the keyframe instant.
            AoiVelocityExtrapolate.Sample(100f, 0f, 5f, 0f, 5000, 5000, out var correctX, out _);
            Assert.AreEqual(100f, correctX, 0.001f);
            // Wrong (do not do this): dt from move_start_ms would add 4s * 5 = 20m.
            var wrong = 100f + 5f * ((5000 - 1000) / 1000f);
            Assert.AreEqual(120f, wrong, 0.001f);
            Assert.AreNotEqual(wrong, correctX);
        }

        [Test]
        public void ClockBias_UpdatesOnlyOnAoiFrame()
        {
            AoiVelocityClock.NoteAoiFrame(serverTimeMs: 10_000, localReceiveMs: 1_000);
            Assert.IsTrue(AoiVelocityClock.HasBias);
            Assert.AreEqual(9_000, AoiVelocityClock.ClockBiasMs);
            Assert.AreEqual(11_000, AoiVelocityClock.EstServerNowMs(2_000));
        }

        [Test]
        public void ProtocolMapper_PassesVelocityFields()
        {
            var src = new EntitySnapshot
            {
                PlayerId = 42,
                Position = new Vec3 { X = 1, Y = 2, Z = 3 },
                Vx = 4.5f,
                Vz = -1.5f,
                MoveStartMs = 12345
            };
            var dto = ProtocolMapper.ToEntityDto(src, 9);
            Assert.AreEqual(4.5f, dto.Vx, 0.001f);
            Assert.AreEqual(-1.5f, dto.Vz, 0.001f);
            Assert.AreEqual(12345, dto.MoveStartMs);
        }

        [Test]
        public void AoiWorld_Copy_KeepsVelocityOnEnter()
        {
            var world = new AoiWorld();
            Assert.IsTrue(world.ApplyDelta(new AoiDeltaDto
            {
                Op = AoiOp.Enter,
                MapInstanceId = 1,
                ServerTimeMs = 5000,
                Entity = new EntitySnapshotDto
                {
                    EntityId = 7,
                    PlayerId = 7,
                    X = 10,
                    Z = 20,
                    Vx = 3f,
                    Vz = 4f,
                    MoveStartMs = 4000,
                    ServerTimeMs = 5000
                }
            }));
            Assert.IsTrue(world.Entities.ContainsKey(7));
            var state = world.Entities[7];
            Assert.AreEqual(3f, state.Vx, 0.001f);
            Assert.AreEqual(4f, state.Vz, 0.001f);
            Assert.AreEqual(4000, state.MoveStartMs);
            Assert.AreEqual(5000, state.ServerTimeMs);
        }
    }
}
