using System.Linq;
using Bastion.Engine;
using Xunit;
using static Bastion.Engine.Tests.Step1Helpers;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// The aim, what is behind it (Arturo, 25/11, cuadro 157; gameplay A2): a shot says what gives its target cover, how many
    /// tiles off it is and whether it is fired stepping out (P5); what is lower than the eyes covers by half (P6).
    /// </summary>
    public class CoverShownTests
    {
        private static GameState Room(string? decorAt4x1)
        {
            var s = StateFrom(new[] { "..........", "....H.....", ".........." });
            s.Decor = Enumerable.Repeat(0, s.Width * s.Height).ToList();
            if (decorAt4x1 != null) s.Decor[Grid.TileIndex(s, 4, 1)] = Decor.DecorCode(decorAt4x1);
            return s;
        }

        [Fact(DisplayName = "cobertura › the shot says the tile and the piece behind its target, the tiles off, and none when flanked (P5)")]
        public void TheShotSaysWhatCovers()
        {
            var s = Room(DecorKinds.Shelf);
            var soldier = AddUnit(s, "s1", TemplateIds.Rookie, 9, 1, extra: (u) => u.Arms = new Arms { Primary = FoundWeaponIds.AssaultRifle, Pistol = false });
            var target = AddUnit(s, "a1", TemplateIds.Trooper, 3, 1);
            var shot = Aim.PreviewShot(s, soldier, target)!;
            Assert.Equal(2, shot.Cover);
            Assert.Equal((4, 1), (shot.CoverTile!.X, shot.CoverTile.Y));
            Assert.Equal(DecorKinds.Shelf, shot.CoverPiece);
            Assert.Contains(shot.Mods, (m) => m.Label == "Cobertura alta" && m.Value == -Content.HighCoverBonus);
            Assert.Equal((int)System.Math.Floor(shot.Distance), shot.Tiles);
            Assert.Equal(6, shot.Tiles);
            // From the other side, flanked: nothing behind it.
            soldier.Pos = P(0, 1);
            var flank = Aim.PreviewShot(s, soldier, target)!;
            Assert.Equal((0, (string?)null), (flank.Cover, flank.CoverPiece));
            Assert.Null(flank.CoverTile);
            // A wall: its kind.
            var w = StateFrom(new[] { "..........", "....#.....", ".........." });
            var a = AddUnit(w, "s1", TemplateIds.Rookie, 9, 1, extra: (u) => u.Arms = new Arms { Primary = FoundWeaponIds.AssaultRifle, Pistol = false });
            var b = AddUnit(w, "a1", TemplateIds.Trooper, 3, 0);
            var wall = Aim.PreviewShot(w, a, b);
            if (wall != null && wall.Cover > 0) Assert.Equal(TileKinds.Wall, wall.CoverPiece);
        }

        [Fact(DisplayName = "cobertura › fired stepping out from beside high cover: the shot says so (P5)")]
        public void ItSaysWhenItStepsOut()
        {
            // The soldier behind a wall that hides the target, looking out beside it.
            var s = StateFrom(new[] { "..........", "......#...", ".........." });
            var soldier = AddUnit(s, "s1", TemplateIds.Rookie, 7, 1, extra: (u) => u.Arms = new Arms { Primary = FoundWeaponIds.AssaultRifle, Pistol = false });
            var target = AddUnit(s, "a1", TemplateIds.Trooper, 1, 1);
            var shot = Aim.PreviewShot(s, soldier, target)!;
            Assert.True(shot.Peeked);
            Assert.False(Grid.SamePos(shot.Origin, soldier.Pos));
            var open = Aim.PreviewShot(s, soldier, AddUnit(s, "a2", TemplateIds.Trooper, 9, 2))!;
            Assert.False(open.Peeked);
        }

        [Fact(DisplayName = "cobertura › what one sees over covers by half, at −20; what one doesn't, as ever (P6)")]
        public void WhatOneSeesOverCoversByHalf()
        {
            foreach (var (piece, cover, bonus) in new[] { (DecorKinds.Crates, 1, Content.LowCoverBonus), (DecorKinds.DrinksFridge, 1, Content.LowCoverBonus), (DecorKinds.Shelf, 2, Content.HighCoverBonus) })
            {
                var s = Room(piece);
                var soldier = AddUnit(s, "s1", TemplateIds.Rookie, 9, 1, extra: (u) => u.Arms = new Arms { Primary = FoundWeaponIds.AssaultRifle, Pistol = false });
                var target = AddUnit(s, "a1", TemplateIds.Trooper, 3, 1);
                var shot = Aim.PreviewShot(s, soldier, target)!;
                Assert.Equal((piece, cover), (shot.CoverPiece, shot.Cover));
                Assert.Contains(shot.Mods, (m) => m.Label.StartsWith("Cobertura") && m.Value == -bonus);
                // Beside it, one steps out to look as ever, and the dead see one behind it as ever (what is seen doesn't change:
                // Producto's review of 0.14.7, Los.CoverShare).
                Assert.Equal(3, Los.PeekOrigins(s, P(5, 1)).Count);
                var zombie = AddUnit(s, "z1", TemplateIds.Zombie, 0, 1);
                Assert.Equal(Content.CoverSight.Full, Los.CoverShare(s, zombie, P(5, 1), Teams.Xcom));
            }
        }
    }
}
