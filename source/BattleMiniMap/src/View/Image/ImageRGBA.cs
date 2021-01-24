

using System;
using System.Drawing;
using TaleWorlds.Library;

namespace BattleMiniMap.View.Image
{
    public class ImageRGBA
    {
        public int Width { get; }
        public int Height { get; }
        public byte[] Image { get; }

        public int Length { get; }

        public ImageRGBA(int width, int height)
        {
            Width = width;
            Height = height;
            Length = width * height * 4;
            Image = new byte[Length];
            for (int i = 0; i < Width; ++i)
            {
                for (int j = 0; j < Height; j++)
                {
                    SetRGBA(i, j, (byte)i, (byte)j, 0, 255);
                }
            }
        }

        public void SetRGBA(int x, int y, byte r, byte g, byte b, byte a = 0)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height)
                throw new ArgumentOutOfRangeException();

            int start = y * Width * 4 + x * 4;
            Image[start] = r;
            Image[start + 1] = g;
            Image[start + 2] = b;
            Image[start + 3] = a;
        }

        public Tuple<byte, byte, byte, byte> GetRGBA(int x, int y)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height)
                throw new ArgumentOutOfRangeException();

            int start = y * Width * 4 + x * 4;
            return new Tuple<byte, byte, byte, byte>(Image[start], Image[start + 1], Image[start + 2], Image[start + 3]);
        }

        public void DrawLine(int xStart, int yStart, int xEnd, int yEnd, byte r, byte g, byte b, byte a = 0)
        {
            if (Math.Abs(xEnd - xStart) > Math.Abs(yEnd - yStart))
            {
                int direction = xEnd > xStart ? 1 : -1;
                for (int x = xStart; direction > 0 ? x <= xEnd : x >= xEnd; x += direction)
                {
                    float y = (float)(x - xStart) / (xEnd - xStart) * (yEnd - yStart) + yStart;
                    int yFloor = MathF.Floor(y);
                    int yCeiling = MathF.Ceiling(y);
                    int yCenter = y - yFloor > yCeiling - y ? yCeiling : yFloor;

                    SetRGBA(x, yCenter, r, g, b, a);
                    SetRGBA(x, yCenter + 1, r, g, b, a);
                    SetRGBA(x, yCenter - 1, r, g, b, a);
                    SetRGBA(x + 1, yCenter, r, g, b, a);
                    SetRGBA(x - 1, yCenter, r, g, b, a);
                    //if (yFloor == yCeiling)
                    //{
                    //    SetRGBA(x, yFloor, r, g, b, a);
                    //}
                    //else
                    //{
                    //    BlendRGBA(x, yFloor, MathF.Sqrt(yCeiling - y), r, g, b, a);
                    //    BlendRGBA(x, yCeiling, MathF.Sqrt(y - yFloor), r, g, b, a);
                    //}
                }
            }
            else
            {
                int direction = yEnd > yStart ? 1 : -1;
                for (int y = yStart; direction > 0 ? y <= yEnd : y >= yEnd; y += direction)
                {
                    float x = (float)(y - yStart) / (yEnd - yStart) * (xEnd - xStart) + xStart;
                    int xFloor = MathF.Floor(x);
                    int xCeiling = MathF.Ceiling(x);
                    int xCenter = x - xFloor > xCeiling - x ? xCeiling : xFloor;

                    SetRGBA(xCenter, y, r, g, b, a);
                    SetRGBA(xCenter + 1, y, r, g, b, a);
                    SetRGBA(xCenter - 1, y, r, g, b, a);
                    SetRGBA(xCenter, y + 1, r, g, b, a);
                    SetRGBA(xCenter, y - 1, r, g, b, a);
                    //if (xFloor == xCeiling)
                    //{
                    //    SetRGBA(xFloor, y, r, g, b, a);
                    //}
                    //else
                    //{
                    //    BlendRGBA(xFloor, y, MathF.Sqrt(xCeiling - x), r, g, b, a);
                    //    BlendRGBA(xCeiling, y, MathF.Sqrt(x - xFloor), r, g, b, a);
                    //}
                }
            }
        }

        //private void BlendRGBA(int x, int y, float factor, byte r, byte g, byte b, byte a = 0)
        //{
        //    factor = MathF.Clamp(factor, 0, 1);
        //    if (factor > 0.707)
        //        factor = 1;
        //    var oldRGBA = GetRGBA(x, y);
        //    var newR = (byte) ((r - oldRGBA.Item1) * factor + oldRGBA.Item1);
        //    var newG = (byte) ((g - oldRGBA.Item2) * factor + oldRGBA.Item2);
        //    var newB = (byte) ((b - oldRGBA.Item3) * factor + oldRGBA.Item3);
        //    var newA = (byte) ((a - oldRGBA.Item4) * factor + oldRGBA.Item4);
        //    SetRGBA(x, y, newR, newG, newB, newA);
        //}
    }
}
