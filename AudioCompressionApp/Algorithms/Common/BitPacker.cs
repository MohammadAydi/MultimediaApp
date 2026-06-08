namespace AudioCompressionApp.Algorithms.Common;

public static class BitPacker {
    public static byte[] PackBits(IReadOnlyList<bool> bits) {
        int byteCount = (bits.Count + 7) / 8;
        byte[] result = new byte[byteCount];

        for (int i = 0; i < bits.Count; i++) {
            if (bits[i]) {
                result[i / 8] |= (byte)(1 << (i % 8));
            }
        }

        return result;
    }

    public static List<bool> UnpackBits(
        byte[] bytes,
        int bitCount) {
        List<bool> result =
            new(bitCount);

        for (int i = 0; i < bitCount; i++) {
            bool bit =
                (bytes[i / 8]
                 & (1 << (i % 8))) != 0;

            result.Add(bit);
        }

        return result;
    }
}