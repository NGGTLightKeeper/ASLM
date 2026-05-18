// Copyright NGGT.LightKeeper. All Rights Reserved.

using Microsoft.Maui.Controls;

namespace ASLM.Services
{
    internal static class IconTintHelper
    {
        internal static Color ResolvePaletteColor(string resourceKey)
        {
            if (Application.Current?.Resources.TryGetValue(resourceKey, out var v) == true && v is Color c)
            {
                return c;
            }

            return Colors.White;
        }
    }
}
