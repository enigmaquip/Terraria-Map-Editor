using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using TEdit.Geometry.Primitives;
using TEdit.Utility;
using Microsoft.Xna.Framework;
using TEditXNA.Terraria;
using TEditXna.Editor;
using TEditXna.Render;
using TEditXNA.Terraria.Objects;

namespace TEditXna.ViewModel
{
    public partial class WorldViewModel
    {
        public void EditDelete()
        {
            if (Selection.IsActive)
            {
                for (int x = Selection.SelectionArea.Left; x < Selection.SelectionArea.Right; x++)
                {
                    for (int y = Selection.SelectionArea.Top; y < Selection.SelectionArea.Bottom; y++)
                    {
                        UndoManager.SaveTile(x, y);
                        CurrentWorld.Tiles[x, y].Reset();

                        Color curBgColor = GetBackgroundColor(y);
                        PixelMap.SetPixelColor(x, y, Render.PixelMap.GetTileColor(CurrentWorld.Tiles[x, y], curBgColor, _showWalls, _showTiles, _showLiquid, _showRedWires, _showBlueWires, _showGreenWires, _showYellowWires));
                    }
                }
                UndoManager.SaveUndo();
            }
        }

        public void EditCopy()
        {
            if (!CanCopy())
                return;
            _clipboard.Buffer = _clipboard.GetSelectionBuffer();
            _clipboard.LoadedBuffers.Add(_clipboard.Buffer);
        }

        public void EditPaste()
        {
            if (!CanPaste())
                return;

            var pasteTool = Tools.FirstOrDefault(t => t.Name == "Paste");
            if (pasteTool != null)
            {
                SetActiveTool(pasteTool);
                PreviewChange();
            }
        }

        public void SetPixel(int x, int y, PaintMode? mode = null, bool? erase = null)
        {
            Tile curTile = CurrentWorld.Tiles[x, y];
            PaintMode curMode = mode ?? TilePicker.PaintMode;
            bool isErase = erase ?? TilePicker.IsEraser;

            switch (curMode)
            {
                case PaintMode.TileAndWall:
                    if (TilePicker.TileStyleActive)
                        SetTile(curTile, isErase);
                    if (TilePicker.WallStyleActive)
                        SetWall(curTile, isErase);
                    if (TilePicker.BrickStyleActive && TilePicker.ExtrasActive)
                        SetPixelAutomatic(curTile, brickStyle: TilePicker.BrickStyle);
                    if (TilePicker.TilePaintActive)
                        SetPixelAutomatic(curTile, tileColor: isErase ? 0 : TilePicker.TileColor);
                    if (TilePicker.WallPaintActive)
                        SetPixelAutomatic(curTile, wallColor: isErase ? 0 : TilePicker.WallColor);
                    if (TilePicker.ExtrasActive)
                        SetPixelAutomatic(curTile, actuator: TilePicker.Actuator, actuatorInActive: TilePicker.ActuatorInActive);
                    break;
                case PaintMode.Wire:
                    if (TilePicker.RedWireActive)
                        SetPixelAutomatic(curTile, wire: !isErase);
                    if (TilePicker.BlueWireActive)
                        SetPixelAutomatic(curTile, wire2: !isErase);
                    if (TilePicker.GreenWireActive)
                        SetPixelAutomatic(curTile, wire3: !isErase);
                    if (TilePicker.YellowWireActive)
                        SetPixelAutomatic(curTile, wire4: !isErase);
                    break;
                case PaintMode.Liquid:
                    SetPixelAutomatic(curTile, liquid: isErase ? (byte)0 : (byte)255, liquidType: TilePicker.LiquidType);
                    break;
                case PaintMode.Track:
                    SetTrack(x, y, curTile, isErase, (TilePicker.TrackMode == TrackMode.Hammer), true);
                    break;
                case PaintMode.Generate:
                    switch (TilePicker.GenerateMode)
                    {
                        case GenerateMode.Pyramid:
                            MakePyramid(x, y);
                            break;
                        case GenerateMode.CloudLake:
                            MakeCloudLake(x, y);
                            break;
                    }
                    break;
            }


            // curTile.BrickStyle = TilePicker.BrickStyle;

            Color curBgColor = GetBackgroundColor(y);
            PixelMap.SetPixelColor(x, y, Render.PixelMap.GetTileColor(CurrentWorld.Tiles[x, y], curBgColor, _showWalls, _showTiles, _showLiquid, _showRedWires, _showBlueWires, _showGreenWires, _showYellowWires));
        }

        private void UpdateRenderWorld()
        {
            Task.Factory.StartNew(
                () =>
                {
                    if (CurrentWorld != null)
                    {
                        for (int y = 0; y < CurrentWorld.TilesHigh; y++)
                        {
                            Color curBgColor = GetBackgroundColor(y);
                            OnProgressChanged(this, new ProgressChangedEventArgs(y.ProgressPercentage(CurrentWorld.TilesHigh), "Calculating Colors..."));
                            for (int x = 0; x < CurrentWorld.TilesWide; x++)
                            {
                                PixelMap.SetPixelColor(x, y, Render.PixelMap.GetTileColor(CurrentWorld.Tiles[x, y], curBgColor, _showWalls, _showTiles, _showLiquid, _showRedWires, _showBlueWires, _showGreenWires, _showYellowWires));
                            }
                        }
                    }
                    OnProgressChanged(this, new ProgressChangedEventArgs(100, "Render Complete"));
                });
        }

        public void UpdateRenderPixel(Vector2Int32 p)
        {
            UpdateRenderPixel(p.X, p.Y);
        }
        public void UpdateRenderPixel(int x, int y)
        {
            Color curBgColor = GetBackgroundColor(y);
            PixelMap.SetPixelColor(x, y, Render.PixelMap.GetTileColor(CurrentWorld.Tiles[x, y], curBgColor, _showWalls, _showTiles, _showLiquid, _showRedWires, _showBlueWires, _showGreenWires, _showYellowWires));
        }

        public void UpdateRenderRegion(Rectangle area)
        {
            Task.Factory.StartNew(
            () =>
            {
                var bounded = new Rectangle(Math.Max(area.Left, 0),
                                                  Math.Max(area.Top, 0),
                                                  Math.Min(area.Width, CurrentWorld.TilesWide - Math.Max(area.Left, 0)),
                                                  Math.Min(area.Height, CurrentWorld.TilesHigh - Math.Max(area.Top, 0)));
                if (CurrentWorld != null)
                {
                    for (int y = bounded.Top; y < bounded.Bottom; y++)
                    {
                        Color curBgColor = GetBackgroundColor(y);
                        OnProgressChanged(this, new ProgressChangedEventArgs(y.ProgressPercentage(CurrentWorld.TilesHigh), "Calculating Colors..."));
                        for (int x = bounded.Left; x < bounded.Right; x++)
                        {
                            PixelMap.SetPixelColor(x, y, Render.PixelMap.GetTileColor(CurrentWorld.Tiles[x, y], curBgColor, _showWalls, _showTiles, _showLiquid, _showRedWires, _showBlueWires, _showGreenWires, _showYellowWires));
                        }
                    }
                }
                OnProgressChanged(this, new ProgressChangedEventArgs(100, "Render Complete"));
            });
        }

        private void SetWall(Tile curTile, bool erase)
        {
            if (TilePicker.WallMaskMode == MaskMode.Off ||
                (TilePicker.WallMaskMode == MaskMode.Match && curTile.Wall == TilePicker.WallMask) ||
                (TilePicker.WallMaskMode == MaskMode.Empty && curTile.Wall == 0) ||
                (TilePicker.WallMaskMode == MaskMode.NotMatching && curTile.Wall != TilePicker.WallMask))
            {
                if (erase)
                    SetPixelAutomatic(curTile, wall: 0);
                else
                    SetPixelAutomatic(curTile, wall: TilePicker.Wall);
            }
        }

        private void SetTile(Tile curTile, bool erase)
        {
            if (TilePicker.TileMaskMode == MaskMode.Off ||
                (TilePicker.TileMaskMode == MaskMode.Match && curTile.Type == TilePicker.TileMask && curTile.IsActive) ||
                (TilePicker.TileMaskMode == MaskMode.Empty && !curTile.IsActive) ||
                (TilePicker.TileMaskMode == MaskMode.NotMatching && (curTile.Type != TilePicker.TileMask || !curTile.IsActive)))
            {
                if (erase)
                    SetPixelAutomatic(curTile, tile: -1);
                else
                    SetPixelAutomatic(curTile, tile: TilePicker.Tile);
            }
        }
        
        private void SetTrack(int x, int y, Tile curTile, bool erase, bool hammer, bool check)
        {
            if (TilePicker.TrackMode == TrackMode.Pressure)
            {
                if (erase)
                    if (curTile.V == 21)
                        curTile.V = 1;
                    else
                    {
                        if (curTile.U >= 20 && curTile.U <= 23)
                            curTile.U -= 20;
                    }
                else
                {
                    if (curTile.V == 1)
                        curTile.V = 21;
                    else
                    {
                        if (curTile.U >= 0 && curTile.U <= 3)
                            curTile.U += 20;
                        if (curTile.U == 14 || curTile.U == 24)
                            curTile.U += 22;
                        if (curTile.U == 15 || curTile.U == 25)
                            curTile.U += 23;
                    }
                }
            }
            else if (TilePicker.TrackMode == TrackMode.Booster)
            {
                if (erase)
                {
                    if (curTile.U == 30 || curTile.U == 31)
                        curTile.U = 1;
                    if (curTile.U == 32 || curTile.U == 34)
                        curTile.U = 8;
                    if (curTile.U == 33 || curTile.U == 35)
                        curTile.U = 9;
                }
                else
                {
                    if (curTile.U == 1)
                        curTile.U = 30;
                    if (curTile.U == 8)
                        curTile.U = 32;
                    if (curTile.U == 9)
                        curTile.U = 33;
                }
            }
            else
            {
                if(erase)
                {
                    int num1 = curTile.U;
                    int num2 = curTile.V;
                    SetPixelAutomatic(curTile, tile: -1, u: 0, v: 0);
                    if (num1 > 0)
                    {
                        switch (Minecart.LeftSideConnection[num1])
                        {
                            case 0: SetTrack(x - 1, y - 1, CurrentWorld.Tiles[x - 1, y - 1], false, false, false); break;
                            case 1: SetTrack(x - 1, y, CurrentWorld.Tiles[x - 1, y], false, false, false); break;
                            case 2: SetTrack(x - 1, y + 1, CurrentWorld.Tiles[x - 1, y + 1], false, false, false); break;
                        }
                        switch (Minecart.RightSideConnection[num1])
                        {
                            case 0: SetTrack(x + 1, y - 1, CurrentWorld.Tiles[x + 1, y - 1], false, false, false); break;
                            case 1: SetTrack(x + 1, y, CurrentWorld.Tiles[x + 1, y], false, false, false); break;
                            case 2: SetTrack(x + 1, y + 1, CurrentWorld.Tiles[x + 1, y + 1], false, false, false); break;
                        }
                    }
                    if (num2 > 0)
                    {
                        switch (Minecart.LeftSideConnection[num2])
                        {
                            case 0: SetTrack(x - 1, y - 1, CurrentWorld.Tiles[x - 1, y - 1], false, false, false); break;
                            case 1: SetTrack(x - 1, y, CurrentWorld.Tiles[x - 1, y], false, false, false); break;
                            case 2: SetTrack(x - 1, y + 1, CurrentWorld.Tiles[x - 1, y + 1], false, false, false); break;
                        }
                        switch (Minecart.RightSideConnection[num2])
                        {
                            case 0: SetTrack(x + 1, y - 1, CurrentWorld.Tiles[x + 1, y - 1], false, false, false); break;
                            case 1: SetTrack(x + 1, y, CurrentWorld.Tiles[x + 1, y], false, false, false); break;
                            case 2: SetTrack(x + 1, y + 1, CurrentWorld.Tiles[x + 1, y + 1], false, false, false); break;
                        }
                    }
                }
                else
                {
                    int num = 0;
                    if (CurrentWorld.Tiles[x - 1, y - 1] != null && CurrentWorld.Tiles[x - 1, y - 1].Type == 314)
                        num++;
                    if (CurrentWorld.Tiles[x - 1, y] != null && CurrentWorld.Tiles[x - 1, y].Type == 314)
                        num += 2;
                    if (CurrentWorld.Tiles[x - 1, y + 1] != null && CurrentWorld.Tiles[x - 1, y + 1].Type == 314)
                        num += 4;
                    if (CurrentWorld.Tiles[x + 1, y - 1] != null && CurrentWorld.Tiles[x + 1, y - 1].Type == 314)
                        num += 8;
                    if (CurrentWorld.Tiles[x + 1, y] != null && CurrentWorld.Tiles[x + 1, y].Type == 314)
                        num += 16;
                    if (CurrentWorld.Tiles[x + 1, y + 1] != null && CurrentWorld.Tiles[x + 1, y + 1].Type == 314)
                        num += 32;
                    int Front = curTile.U;
                    int Back = curTile.V;
                    int num4;
                    if (Front >= 0 && Front < Minecart.TrackType.Length)
                        num4 = Minecart.TrackType[Front];
                    else
                        num4 = 0;
                    int num5 = -1;
                    int num6 = -1;
                    int[] array = Minecart.TrackSwitchOptions[num];
                    if (!hammer)
                    {
                        if (curTile.Type != 314)
                        {
                            curTile.Type = (ushort)314;
                            curTile.IsActive = true;
                            Front = 0;
                            Back = -1;
                        }
                        int num7 = -1;
                        int num8 = -1;
                        bool flag = false;
                        for (int k = 0; k < array.Length; k++)
                        {
                            int num9 = array[k];
                            if (Back == array[k])
                                num6 = k;
                            if (Minecart.TrackType[num9] == num4)
                            {
                                if (Minecart.LeftSideConnection[num9] == -1 || Minecart.RightSideConnection[num9] == -1)
                                {
                                    if (Front == array[k])
                                    {
                                        num5 = k;
                                        flag = true;
                                    }
                                    if (num7 == -1)
                                        num7 = k;
                                }
                                else
                                {
                                    if (Front == array[k])
                                    {
                                        num5 = k;
                                        flag = false;
                                    }
                                    if (num8 == -1)
                                        num8 = k;
                                }
                            }
                        }
                        if (num8 != -1)
                        {
                            if (num5 == -1 || flag)
                                num5 = num8;
                        }
                        else
                        {
                            if (num5 == -1)
                            {
                                if (num4 == 2 || num4 == 1)
                                    return;
                                num5 = num7;
                            }
                            num6 = -1;
                        }
                    }
                    else if (hammer && curTile.Type == 314)
                    {
                        for (int l = 0; l < array.Length; l++)
                        {
                            if (Front == array[l])
                                num5 = l;
                            if (Back == array[l])
                                num6 = l;
                        }
                        int num10 = 0;
                        int num11 = 0;
                        for (int m = 0; m < array.Length; m++)
                        {
                            if (Minecart.TrackType[array[m]] == num4)
                            {
                                if (Minecart.LeftSideConnection[array[m]] == -1 || Minecart.RightSideConnection[array[m]] == -1)
                                    num11++;
                                else
                                    num10++;
                            }
                        }
                        if (num10 < 2 && num11 < 2)
                            return;
                        bool flag2 = num10 == 0;
                        bool flag3 = false;
                        if (!flag2)
                        {
                            while (!flag3)
                            {
                                num6++;
                                if (num6 >= array.Length)
                                {
                                    num6 = -1;
                                    break;
                                }
                                if ((Minecart.LeftSideConnection[array[num6]] != Minecart.LeftSideConnection[array[num5]] || Minecart.RightSideConnection[array[num6]] != Minecart.RightSideConnection[array[num5]]) && Minecart.TrackType[array[num6]] == num4 && Minecart.LeftSideConnection[array[num6]] != -1 && Minecart.RightSideConnection[array[num6]] != -1)
                                    flag3 = true;
                            }
                        }
                        if (!flag3)
                        {
                            while (true)
                            {
                                num5++;
                                if (num5 >= array.Length)
                                    break;
                                if (Minecart.TrackType[array[num5]] == num4 && (Minecart.LeftSideConnection[array[num5]] == -1 || Minecart.RightSideConnection[array[num5]] == -1) == flag2)
                                    goto IL_100;
                            }
                            num5 = -1;
                            while (true)
                            {
                                num5++;
                                if (Minecart.TrackType[array[num5]] == num4)
                                {
                                    if ((Minecart.LeftSideConnection[array[num5]] == -1 || Minecart.RightSideConnection[array[num5]] == -1) == flag2)
                                        break;
                                }
                            }
                        }
                    }
                    IL_100:
                    if (num5 == -1)
                        curTile.U = 0;
                    else
                    {
                        curTile.U = (short)array[num5];
                        if (check)
                        {
                            switch (Minecart.LeftSideConnection[curTile.U])
                            {
                                case 0: SetTrack(x - 1, y - 1, CurrentWorld.Tiles[x - 1, y - 1], false, false, false); break;
                                case 1: SetTrack(x - 1, y, CurrentWorld.Tiles[x - 1, y], false, false, false); break;
                                case 2: SetTrack(x - 1, y + 1, CurrentWorld.Tiles[x - 1, y + 1], false, false, false); break;
                            }
                            switch (Minecart.RightSideConnection[curTile.U])
                            {
                                case 0: SetTrack(x + 1, y - 1, CurrentWorld.Tiles[x + 1, y - 1], false, false, false); break;
                                case 1: SetTrack(x + 1, y, CurrentWorld.Tiles[x + 1, y], false, false, false); break;
                                case 2: SetTrack(x + 1, y + 1, CurrentWorld.Tiles[x + 1, y + 1], false, false, false); break;
                            }
                        }
                    }
                    if (num6 == -1)
                        curTile.V = -1;
                    else
                    {
                        curTile.V = (short)array[num6];
                        if (check)
                        {
                            switch (Minecart.LeftSideConnection[curTile.V])
                            {
                                case 0: SetTrack(x - 1, y - 1, CurrentWorld.Tiles[x - 1, y - 1], false, false, false); break;
                                case 1: SetTrack(x - 1, y, CurrentWorld.Tiles[x - 1, y], false, false, false); break;
                                case 2: SetTrack(x - 1, y + 1, CurrentWorld.Tiles[x - 1, y + 1], false, false, false); break;
                            }
                            switch (Minecart.RightSideConnection[curTile.V])
                            {
                                case 0: SetTrack(x + 1, y - 1, CurrentWorld.Tiles[x + 1, y - 1], false, false, false); break;
                                case 1: SetTrack(x + 1, y, CurrentWorld.Tiles[x + 1, y], false, false, false); break;
                                case 2: SetTrack(x + 1, y + 1, CurrentWorld.Tiles[x + 1, y + 1], false, false, false); break;
                            }
                        }
                    }
                }
            }
        }

        private void SetPixelAutomatic(Tile curTile,
                                       int? tile = null,
                                       int? wall = null,
                                       byte? liquid = null,
                                       LiquidType? liquidType = null,
                                       bool? wire = null,
                                       short? u = null,
                                       short? v = null,
                                       bool? wire2 = null,
                                       bool? wire3 = null,
                                       bool? wire4 = null,
                                       BrickStyle? brickStyle = null,
                                       bool? actuator = null, bool? actuatorInActive = null,
                                       int? tileColor = null,
                                       int? wallColor = null)
        {
            // Set Tile Data
            if (u != null)
                curTile.U = (short)u;
            if (v != null)
                curTile.V = (short)v;

            if (tile != null)
            {
                if (tile == -1)
                {
                    curTile.Type = 0;
                    curTile.IsActive = false;
                }
                else
                {
                    curTile.Type = (ushort)tile;
                    curTile.IsActive = true;
                }
            }

            if (actuator != null && curTile.IsActive)
            {
                curTile.Actuator = (bool)actuator;
            }

            if (actuatorInActive != null && curTile.IsActive)
            {
                curTile.InActive = (bool)actuatorInActive;
            }

            if (brickStyle != null && TilePicker.BrickStyleActive)
            {
                curTile.BrickStyle = (BrickStyle)brickStyle;
            }

            if (wall != null)
                curTile.Wall = (byte)wall;

            if (liquid != null)
            {
                curTile.LiquidAmount = (byte)liquid;
            }

            if (liquidType != null)
            {
                curTile.LiquidType = (LiquidType)liquidType;
            }

            if (wire != null)
                curTile.WireRed = (bool)wire;

            if (wire2 != null)
                curTile.WireBlue = (bool)wire2;

            if (wire3 != null)
                curTile.WireGreen = (bool)wire3;

            if (wire4 != null)
                curTile.WireYellow = (bool)wire4;

            if (tileColor != null)
            {
                if (curTile.IsActive)
                {
                    curTile.TileColor = (byte)tileColor;
                }
                else
                {
                    curTile.TileColor = (byte)0;
                }
            }

            if (wallColor != null)
            {
                if (curTile.Wall != 0)
                {
                    curTile.WallColor = (byte)wallColor;
                }
                else
                {
                    curTile.WallColor = (byte)0;
                }
            }

            if (curTile.IsActive)
                if (World.TileProperties[curTile.Type].IsSolid)
                    curTile.LiquidAmount = 0;
        }

        private PixelMapManager RenderEntireWorld()
        {
            var pixels = new PixelMapManager();
            if (CurrentWorld != null)
            {
                pixels.InitializeBuffers(CurrentWorld.TilesWide, CurrentWorld.TilesHigh);

                for (int y = 0; y < CurrentWorld.TilesHigh; y++)
                {
                    Color curBgColor = GetBackgroundColor(y);
                    OnProgressChanged(this, new ProgressChangedEventArgs(y.ProgressPercentage(CurrentWorld.TilesHigh), "Calculating Colors..."));
                    for (int x = 0; x < CurrentWorld.TilesWide; x++)
                    {
                        if (y > CurrentWorld.TilesHigh || x > CurrentWorld.TilesWide)
                            throw new IndexOutOfRangeException(string.Format("Error with world format tile [{0},{1}] is not a valid location. World file version: {2}", x, y, CurrentWorld.Version));
                        pixels.SetPixelColor(x, y, Render.PixelMap.GetTileColor(CurrentWorld.Tiles[x, y], curBgColor, _showWalls, _showTiles, _showLiquid, _showRedWires, _showBlueWires, _showGreenWires, _showYellowWires));
                    }
                }
            }
            OnProgressChanged(this, new ProgressChangedEventArgs(100, "Render Complete"));
            return pixels;
        }

        public Color GetBackgroundColor(int y)
        {
            if (y < 80)
                return World.GlobalColors["Space"];
            else if (y > CurrentWorld.TilesHigh - 192)
                return World.GlobalColors["Hell"];
            else if (y > CurrentWorld.RockLevel)
                return World.GlobalColors["Rock"];
            else if (y > CurrentWorld.GroundLevel)
                return World.GlobalColors["Earth"];
            else 
                return World.GlobalColors["Sky"];
        }
        
        private void MakePyramid(int x, int y)
        {
            Random random = new Random();
            ushort pytile = 151;
            int num3 = random.Next(9, 13);
            int num4 = 1;
            int num5 = y + random.Next(75, 125);
            for (int i = y; i < num5; i++)
            {
                for (int j = x - num4; j < x + num4 - 1 ; j++)
                {
                    Tile tile = new Tile();
                    tile.Type = pytile;
                    tile.IsActive = true;
                    tile.BrickStyle = BrickStyle.Full;
                    CurrentWorld.Tiles[j, i] = tile;
                    UpdateRenderPixel(j, i);
                }
                num4++;
            }
            for (int m = x - num4 - 5; m <= x + num4 + 5; m++)
            {
                for (int n = y - 1; n <= num5 + 1; n++)
                {
                    bool flag = true;
                    for (int num6 = m - 1; num6 <= m + 1; num6++)
                    {
                        for (int num7 = n - 1; num7 <= n + 1; num7++)
                        {
                            if (CurrentWorld.Tiles[num6, num7].Type != pytile)
                            {
                                flag = false;
                            }
                        }
                    }
                    if (flag)
                    {
                        CurrentWorld.Tiles[m, n].Wall = 34;
                    }
                }
            }
            int num8 = 1;
            if (random.Next(2) == 0)
            {
                num8 = -1;
            }
            int num9 = x - num3 * num8;
            int num10 = y + num3;
            int num11 = random.Next(5, 8);
            bool flag2 = true;
            int num12 = random.Next(20, 30);
            while (flag2)
            {
                flag2 = false;
                for (int num13 = num10; num13 <= num10 + num11; num13++)
                {
                    int num14 = num9;
                    if (CurrentWorld.Tiles[num14, num13].Type == pytile)
                    {
                        CurrentWorld.Tiles[num14, num13 + 1].Wall = 34;
                        CurrentWorld.Tiles[num14 + num8, num13].Wall = 34;
                        if (CurrentWorld.Tiles[num14, num13 - 1].Type == 53)
                        {
                            CurrentWorld.Tiles[num14, num13].Type = 53;
                            CurrentWorld.Tiles[num14, num13].IsActive = true;
                            CurrentWorld.Tiles[num14, num13].BrickStyle = BrickStyle.Full;
                        }
                        else
                            CurrentWorld.Tiles[num14, num13].IsActive = false;
                        UpdateRenderPixel(num14, num13);
                        flag2 = true;
                    }
                }
                num9 -= num8;
            }
            num9 = x - num3 * num8;
            bool flag4 = true;
            bool flag5 = false;
            flag2 = true;
            while (flag2)
            {
                for (int num15 = num10; num15 <= num10 + num11; num15++)
                {
                    int num16 = num9;
                    CurrentWorld.Tiles[num16, num15].IsActive = false;
                    UpdateRenderPixel(num16, num15);
                }
                num9 += num8;
                num10++;
                num12--;
                if (num10 >= num5 - num11 * 2)
                {
                    num12 = 10;
                }
                if (num12 <= 0)
                {
                    bool flag6 = false;
                    if (!flag4 && !flag5)
                    {
                        flag5 = true;
                        flag6 = true;
                        int num17 = random.Next(7, 13);
                        int num18 = random.Next(23, 28);
                        int num19 = num18;
                        int num20 = num9;
                        while (num18 > 0)
                        {
                            for (int num21 = num10 - num17 + num11; num21 <= num10 + num11; num21++)
                            {
                                if (num18 == num19 || num18 == 1)
                                {
                                    if (num21 >= num10 - num17 + num11 + 2)
                                    {
                                        CurrentWorld.Tiles[num9, num21].IsActive = false;
                                    }
                                }
                                else if (num18 == num19 - 1 || num18 == 2 || num18 == num19 - 2 || num18 == 3)
                                {
                                    if (num21 >= num10 - num17 + num11 + 1)
                                    {
                                        CurrentWorld.Tiles[num9, num21].IsActive = false;
                                    }
                                }
                                else
                                {
                                    CurrentWorld.Tiles[num9, num21].IsActive = false;
                                }
                                UpdateRenderPixel(num9, num21);
                            }
                            num18--;
                            num9 += num8;
                        }
                        int num22 = num9 - num8;
                        int num23 = num22;
                        int num24 = num20;
                        if (num22 > num20)
                        {
                            num23 = num20;
                            num24 = num22;
                        }
                        int num25 = random.Next(3);
                        SetSprite((num23 + num24) / 2, num10 + num11 - 1, 21, 36, 0);
                        Chest chest = new Chest((num23 + num24)/2, num10 + num11 - 1);
                        int slot = 0;
                        while(slot == 0)
                        {
                            switch(num25)
                            {
                                case 0:
                                    chest.Items[slot].NetId = 857;
                                    break;
                                case 1:
                                    chest.Items[slot].NetId = 848;
                                    slot++;
                                    chest.Items[slot].NetId = 866;
                                    break;
                                case 2:
                                    chest.Items[slot].NetId = 934;
                                    break;
                            }
                            slot++;
                        }
                        CurrentWorld.Chests.Add(chest);
                        // add back treasure code
                        int num26 = random.Next(1, 10);
                        int j2 = num10 + num11;
                        for (int num27 = 0; num27 < num26; num27++)
                        {
                            int i2 = random.Next(num23, num24);
                            int pile = random.Next(0, 3);
                            if(!CurrentWorld.Tiles[i2, j2].IsActive && !CurrentWorld.Tiles[i2 + 1, j2].IsActive)
                            {
                                SetSprite(i2, j2, 185, (576 + pile * 36), 18);
                                SetSprite(i2 + 1, j2, 185, (576 + 18 + pile * 36), 18);
                            }
                        }
                        SetSprite(num23 + 2, num10 - num17 + num11 + 1, 91, random.Next(4,7) * 18, 0);
                        SetSprite(num23 + 3, num10 - num17 + num11, 91, random.Next(4,7) * 18, 0);
                        SetSprite(num24 - 2, num10 - num17 + num11 + 1, 91, random.Next(4,7) * 18, 0);
                        SetSprite(num24 - 3, num10 - num17 + num11, 91, random.Next(4,7) * 18, 0);
                        for (int num28 = num23; num28 <= num24; num28++)
                        {
                            int potU = random.Next(0, 3);
                            int potV = random.Next(25, 28);
                            if (!CurrentWorld.Tiles[num28, j2].IsActive && !CurrentWorld.Tiles[num28 + 1, j2].IsActive)
                            {
                                SetSprite(num28, j2 - 1, 28, potU * 36, potV * 36);
                            }
                        }
                    }
                    if (flag4)
                    {
                        flag4 = false;
                        num8 *= -1;
                        num12 = random.Next(15, 20);
                    }
                    else if (flag6)
                    {
                        num12 = random.Next(10, 15);
                    }
                    else
                    {
                        num8 *= -1;
                        num12 = random.Next(20, 40);
                    }
                }
                if (num10 >= num5 - num11)
                {
                    flag2 = false;
                }
            }
            int num29 = random.Next(100, 200);
            int num30 = random.Next(500, 800);
            flag2 = true;
            int num31 = num11;
            num12 = random.Next(10, 50);
            if (num8 == 1)
            {
                num9 -= num31;
            }
            int num32 = random.Next(5, 10);
            while (flag2)
            {
                num29--;
                num30--;
                num12--;
                for (int num33 = num9 - num32 - random.Next(0, 2); num33 <= num9 + num31 + num32 + random.Next(0, 2); num33++)
                {
                    int num34 = num10;
                    if (num33 >= num9 && num33 <= num9 + num31)
                    {
                        CurrentWorld.Tiles[num33, num34].IsActive = false;
                    }
                    else
                    {
                        CurrentWorld.Tiles[num33, num34].Type = pytile;
                        CurrentWorld.Tiles[num33, num34].IsActive = true;
                        CurrentWorld.Tiles[num33, num34].BrickStyle = BrickStyle.Full;
                    }
                    if (num33 >= num9 - 1 && num33 <= num9 + 1 + num31)
                    {
                        CurrentWorld.Tiles[num33, num34].Wall = 34;
                    }
                    UpdateRenderPixel(num33, num34);
                }
                num10++;
                num9 += num8;
                if (num29 <= 0)
                {
                    flag2 = false;
                    for (int num35 = num9 + 1; num35 <= num9 + num31 - 1; num35++)
                    {
                        if (CurrentWorld.Tiles[num35, num10].IsActive)
                        {
                            flag2 = true;
                        }
                    }
                }
                if (num12 < 0)
                {
                    num12 = random.Next(10, 50);
                    num8 *= -1;
                }
                if (num30 <= 0)
                {
                    flag2 = false;
                }
            }
        }
        
        private void MakeCloudLake(int i, int j)
        {
            Random random = new Random();
            double num = (double)random.Next(100, 150);
            float num2 = (float)random.Next(20, 30);
            int num3 = i;
            int num4 = i;
            int num5 = i;
            int num6 = j;
            float valueX = (float)i;
            float valueY = (float)j;
            float value2X = (float)random.Next(-20, 21) * 0.2f;
            while (value2X > -2f && value2X < 2f)
            {
                value2X = (float)random.Next(-20, 21) * 0.2f;
            }
            float value2Y = (float)random.Next(-20, -10) * 0.02f;
            while (num > 0.0 && num2 > 0f)
            {
                num -= (double)random.Next(4);
                num2 -= 1f;
                int num7 = (int)((double)valueX - num * 0.5);
                int num8 = (int)((double)valueX + num * 0.5);
                int num9 = (int)((double)valueY - num * 0.5);
                int num10 = (int)((double)valueY + num * 0.5);
                if (num7 < 0)
                {
                    num7 = 0;
                }
                if (num8 > CurrentWorld.TilesWide)
                {
                    num8 = CurrentWorld.TilesWide;
                }
                if (num9 < 0)
                {
                    num9 = 0;
                }
                if (num10 > CurrentWorld.TilesHigh)
                {
                    num10 = CurrentWorld.TilesHigh;
                }
                double num11 = num * (double)random.Next(80, 120) * 0.01;
                float num12 = valueY + 1f;
                for (int k = num7; k < num8; k++)
                {
                    if (random.Next(2) == 0)
                    {
                        num12 += (float)random.Next(-1, 2);
                    }
                    if (num12 < valueY)
                    {
                        num12 = valueY;
                    }
                    if (num12 > valueY + 2f)
                    {
                        num12 = valueY + 2f;
                    }
                    for (int l = num9; l < num10; l++)
                    {
                        if ((float)l > num12)
                        {
                            float num13 = Math.Abs((float)k - valueX);
                            float num14 = Math.Abs((float)l - valueY) * 3f;
                            double num15 = Math.Sqrt((double)(num13 * num13 + num14 * num14));
                            if (num15 < num11 * 0.4)
                            {
                                if (k < num3)
                                {
                                    num3 = k;
                                }
                                if (k > num4)
                                {
                                    num4 = k;
                                }
                                if (l < num5)
                                {
                                    num5 = l;
                                }
                                if (l > num6)
                                {
                                    num6 = l;
                                }
                                CurrentWorld.Tiles[k, l].IsActive = true;
                                CurrentWorld.Tiles[k, l].Type = 189;
                                CurrentWorld.Tiles[k, l].BrickStyle = BrickStyle.Full;
                                UpdateRenderPixel(k, l);
                            }
                        }
                    }
                }
                valueX += value2X;
                valueY += value2Y;
                value2X += (float)random.Next(-20, 21) * 0.05f;
                if (value2X > 1f)
                {
                    value2X = 1f;
                }
                if (value2X < -1f)
                {
                    value2X = -1f;
                }
                if ((double)value2Y > 0.2)
                {
                    value2Y = -0.2f;
                }
                if ((double)value2Y < -0.2)
                {
                    value2Y = -0.2f;
                }
            }
            int m = num3;
            int num17;
            for (m += random.Next(5); m < num4; m += random.Next(num17, (int)((double)num17 * 1.5)))
            {
                int num16 = num6;
                while (!CurrentWorld.Tiles[m, num16].IsActive)
                {
                    num16--;
                }
                num16 += random.Next(-3, 4);
                num17 = random.Next(4, 8);
                int num18 = 189;
                if (random.Next(4) == 0)
                {
                    num18 = 196;
                }
                for (int n = m - num17; n <= m + num17; n++)
                {
                    for (int num19 = num16 - num17; num19 <= num16 + num17; num19++)
                    {
                        if (num19 > num5)
                        {
                            float num20 = (float)Math.Abs(n - m);
                            float num21 = (float)(Math.Abs(num19 - num16) * 2);
                            double num22 = Math.Sqrt((double)(num20 * num20 + num21 * num21));
                            if (num22 < (double)(num17 + random.Next(2)))
                            {
                                CurrentWorld.Tiles[n, num19].IsActive = true;
                                CurrentWorld.Tiles[n, num19].Type = (ushort)num18;
                                CurrentWorld.Tiles[n, num19].BrickStyle = BrickStyle.Full;
                                UpdateRenderPixel(n, num19);
                            }
                        }
                    }
                }
            }
            num = (double)random.Next(80, 95);
            num2 = (float)random.Next(10, 15);
            valueX = (float)i;
            valueY = (float)num5;
            value2X = (float)random.Next(-20, 21) * 0.2f;
            while (value2X > -2f && value2X < 2f)
            {
                value2X = (float)random.Next(-20, 21) * 0.2f;
            }
            value2Y = (float)random.Next(-20, -10) * 0.02f;
            while (num > 0.0 && num2 > 0f)
            {
                num -= (double)random.Next(4);
                num2 -= 1f;
                int num7 = (int)((double)valueX - num * 0.5);
                int num8 = (int)((double)valueX + num * 0.5);
                int num9 = num5 - 1;
                int num10 = (int)((double)valueY + num * 0.5);
                if (num7 < 0)
                {
                    num7 = 0;
                }
                if (num8 > CurrentWorld.TilesWide)
                {
                    num8 = CurrentWorld.TilesWide;
                }
                if (num9 < 0)
                {
                    num9 = 0;
                }
                if (num10 > CurrentWorld.TilesHigh)
                {
                    num10 = CurrentWorld.TilesHigh;
                }
                double num11 = num * (double)random.Next(80, 120) * 0.01;
                float num23 = valueY + 1f;
                for (int num24 = num7; num24 < num8; num24++)
                {
                    if (random.Next(2) == 0)
                    {
                        num23 += (float)random.Next(-1, 2);
                    }
                    if (num23 < valueY)
                    {
                        num23 = valueY;
                    }
                    if (num23 > valueY + 2f)
                    {
                        num23 = valueY + 2f;
                    }
                    for (int num25 = num9; num25 < num10; num25++)
                    {
                        if ((float)num25 > num23)
                        {
                            float num26 = Math.Abs((float)num24 - valueX);
                            float num27 = Math.Abs((float)num25 - valueY) * 3f;
                            double num28 = Math.Sqrt((double)(num26 * num26 + num27 * num27));
                            if (num28 < num11 * 0.4 && CurrentWorld.Tiles[num24, num25].Type == 189)
                            {
                                CurrentWorld.Tiles[num24, num25].IsActive = false;
                                CurrentWorld.Tiles[num24, num25].LiquidAmount = 255;
                                CurrentWorld.Tiles[num24, num25].LiquidType = LiquidType.Water;
                                UpdateRenderPixel(num24, num25);
                            }
                        }
                    }
                }
                valueX += value2X;
                valueY += value2Y;
                value2X += (float)random.Next(-20, 21) * 0.05f;
                if (value2X > 1f)
                {
                    value2X = 1f;
                }
                if (value2X < -1f)
                {
                    value2X = -1f;
                }
                if ((double)value2Y > 0.2)
                {
                    value2Y = -0.2f;
                }
                if ((double)value2Y < -0.2)
                {
                    value2Y = -0.2f;
                }
            }
            for (int num29 = num3 - 20; num29 <= num4 + 20; num29++)
            {
                for (int num30 = num5 - 20; num30 <= num6 + 20; num30++)
                {
                    bool flag = true;
                    for (int num31 = num29 - 1; num31 <= num29 + 1; num31++)
                    {
                        for (int num32 = num30 - 1; num32 <= num30 + 1; num32++)
                        {
                            if (!CurrentWorld.Tiles[num31, num32].IsActive)
                            {
                                flag = false;
                            }
                        }
                    }
                    if (flag)
                    {
                        CurrentWorld.Tiles[num29, num30].Wall = 73;
                        UpdateRenderPixel(num29, num30);
                    }
                }
            }
            for (int num33 = num3; num33 <= num4; num33++)
            {
                int num34 = num5 - 10;
                while (!CurrentWorld.Tiles[num33, num34 + 1].IsActive)
                {
                    num34++;
                }
                if (num34 < num6 && CurrentWorld.Tiles[num33, num34 + 1].Type == 189)
                {
                    if (random.Next(10) == 0)
                    {
                        int num35 = random.Next(1, 3);
                        for (int num36 = num33 - num35; num36 <= num33 + num35; num36++)
                        {
                            if (CurrentWorld.Tiles[num36, num34].Type == 189)
                            {
                                CurrentWorld.Tiles[num36, num34].IsActive = false;
                                CurrentWorld.Tiles[num36, num34].LiquidAmount = 255;
                                CurrentWorld.Tiles[num36, num34].LiquidType = LiquidType.Water;
                                UpdateRenderPixel(num36, num34);
                            }
                            if (CurrentWorld.Tiles[num36, num34 + 1].Type == 189)
                            {
                                CurrentWorld.Tiles[num36, num34 + 1].IsActive = false;
                                CurrentWorld.Tiles[num36, num34 + 1].LiquidAmount = 255;
                                CurrentWorld.Tiles[num36, num34 + 1].LiquidType = LiquidType.Water;
                                UpdateRenderPixel(num36, num34 + 1);
                            }
                            if (num36 > num33 - num35 && num36 < num33 + 2 && CurrentWorld.Tiles[num36, num34 + 2].Type == 189)
                            {
                                CurrentWorld.Tiles[num36, num34 + 2].IsActive = false;
                                CurrentWorld.Tiles[num36, num34 + 2].LiquidAmount = 255;
                                CurrentWorld.Tiles[num36, num34 + 2].LiquidType = LiquidType.Water;
                                UpdateRenderPixel(num36, num34 + 2);
                            }
                        }
                    }
                    if (random.Next(5) == 0)
                    {
                        CurrentWorld.Tiles[num33, num34].LiquidAmount = 255;
                    }
                    CurrentWorld.Tiles[num33, num34].LiquidType = LiquidType.Water;
                }
            }
            int num37 = random.Next(1, 4);
            for (int num38 = 0; num38 <= num37; num38++)
            {
                int num39 = random.Next(num3 - 5, num4 + 5);
                int num40 = num5 - random.Next(20, 40);
                int num41 = random.Next(4, 8);
                int num42 = 189;
                if (random.Next(4) != 0)
                {
                    num42 = 196;
                }
                for (int num43 = num39 - num41; num43 <= num39 + num41; num43++)
                {
                    for (int num44 = num40 - num41; num44 <= num40 + num41; num44++)
                    {
                        float num45 = (float)Math.Abs(num43 - num39);
                        float num46 = (float)(Math.Abs(num44 - num40) * 2);
                        double num47 = Math.Sqrt((double)(num45 * num45 + num46 * num46));
                        if (num47 < (double)(num41 + random.Next(-1, 2)))
                        {
                            CurrentWorld.Tiles[num43, num44].IsActive = true;
                            CurrentWorld.Tiles[num43, num44].Type = (ushort)num42;
                            CurrentWorld.Tiles[num43, num44].BrickStyle = BrickStyle.Full;
                            UpdateRenderPixel(num43, num44);
                        }
                    }
                }
                for (int num48 = num39 - num41 + 2; num48 <= num39 + num41 - 2; num48++)
                {
                    int num49 = num40 - num41;
                    while (!CurrentWorld.Tiles[num48, num49].IsActive)
                    {
                        num49++;
                    }
                    CurrentWorld.Tiles[num48, num49].IsActive = false;
                    CurrentWorld.Tiles[num48, num49].LiquidAmount = 255;
                    CurrentWorld.Tiles[num48, num49].LiquidType = LiquidType.Water;
                    UpdateRenderPixel(num48, num49);
                }
            }
        }
        
        private void SetSprite(int i, int j, int type, int u, int v)
        {
            for(int x = 0; x < World.TileProperties[type].FrameSize.X; x++)
            {
                for(int y = 0; y < World.TileProperties[type].FrameSize.Y; y++)
                {
                    CurrentWorld.Tiles[i + x, j + y].IsActive = true;
                    CurrentWorld.Tiles[i + x, j + y].Type = (ushort)type;
                    CurrentWorld.Tiles[i + x, j + y].U = (short)(u + (x * (World.TileProperties[type].TextureGrid.X + 2)));
                    CurrentWorld.Tiles[i + x, j + y].V = (short)(v + (y * (World.TileProperties[type].TextureGrid.Y + 2)));
                    UpdateRenderPixel(i + x, j + y);
                }
            }
        }
    }
}
 