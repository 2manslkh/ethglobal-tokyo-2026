using System;

namespace Tagtag.UI
{
    public static class BookPaging
    {
        public const int Columns = 4;
        public const int Rows = 5;
        public const int PageSize = Columns * Rows;

        public static int PageCount(int itemCount)
        {
            return Math.Max(1, (Math.Max(0, itemCount) + PageSize - 1) / PageSize);
        }

        public static int ClampPage(int page, int itemCount)
        {
            return Math.Max(0, Math.Min(page, PageCount(itemCount) - 1));
        }

        public static int IndexAt(int page, int slot, int itemCount)
        {
            if (slot < 0 || slot >= PageSize || itemCount < 0)
            {
                return -1;
            }

            int index = ClampPage(page, itemCount) * PageSize + slot;
            return index < itemCount ? index : -1;
        }

        public static int PageAfterSwipe(int page, int itemCount, float deltaX, float deltaY, float threshold = 32f)
        {
            if (Math.Abs(deltaX) < threshold || Math.Abs(deltaX) <= Math.Abs(deltaY))
            {
                return ClampPage(page, itemCount);
            }

            return ClampPage(page + (deltaX < 0f ? 1 : -1), itemCount);
        }

        public static bool IsTap(float deltaX, float deltaY, float maxTravel = 12f)
        {
            return deltaX * deltaX + deltaY * deltaY <= maxTravel * maxTravel;
        }
    }
}
