// Copyright 2026 zhanglinshuia-code
// SPDX-License-Identifier: Apache-2.0

namespace ZRList.Samples.Support
{
    public static class DemoQuantityText
    {
        // Quantities are business values; retain one label per distinct value, not per cell.
        private static readonly string[] s_labels = new string[512];

        public static string Get(int quantity)
        {
            if (quantity < 0 || quantity >= s_labels.Length) {
                return "x" + quantity;
            }

            return s_labels[quantity] ??= "x" + quantity;
        }
    }
}
