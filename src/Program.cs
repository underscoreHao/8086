using System.Text;

class Program
{
    private static readonly Dictionary<int, string> instructionEncodings = new()
    {
        { 0b100010, "mov" },
        { 0b1011, "mov" }
    };
    private static int[] modFieldEncodings = new int[] { 0b00, 0b01, 0b10, 0b11 };

    private static readonly Dictionary<int, string[]> regFieldMemoryModeEncoding = new()
    {
        { 0b000, new[] {"al", "ax"} },
        { 0b001, new[] {"cl", "cx"} },
        { 0b010, new[] {"dl", "dx"} },
        { 0b011, new[] {"bl", "bx"} },
        { 0b100, new[] {"ah", "sp"} },
        { 0b101, new[] {"ch", "bp"} },
        { 0b110, new[] {"dh", "si"} },
        { 0b111, new[] {"bh", "di"} },
    };

    private static readonly Dictionary<int, string[]> rmFieldEffectiveAddressEncoding = new()
    {
        { (0b000), new[] {"bx + si"} },
        { (0b001), new[] {"bx + di"} },
        { (0b010), new[] {"bp + si"} },
        { (0b011), new[] {"bp + di"} },
        { (0b100), new[] {"si"} },
        { (0b101), new[] {"di"} },
        { (0b110), new[] {"bp"} }, // Direct 16-bit displacement
        { (0b111), new[] {"bx"} },
    };

    static void Main(string[] args)
    {
        Console.WriteLine("8086 v0.1");
        Console.WriteLine();

        using var stdin = Console.OpenStandardInput();
        using var stdout = Console.OpenStandardOutput();

        byte[] buffer = new byte[256];
        int bufferSize = stdin.Read(buffer, 0, buffer.Length);

        var stringBuilder = new StringBuilder();
        stringBuilder.AppendLine("bits 16\n");


        string src = string.Empty;
        string dest = string.Empty;

        int i = 0;
        while (i < bufferSize)
        {
            var opCode = GetOpCode(buffer[i]);
            var opRange = GetOpRange(opCode, ref buffer, i);
            var curInstruction = buffer[opRange];

#if DEBUG
            foreach (var b in curInstruction)
                Console.Write($"{Convert.ToString(b, 2)}, ");

            Console.WriteLine();
#endif

            switch (opCode)
            {
                case 0b1011:
                    (dest, src) = ImmediateToRegister(curInstruction);
                    break;
                case 0b100010:
                    (dest, src) = RegMemToFromRegister(curInstruction);
                    break;
            }

            i = opRange.End.Value;

            Console.WriteLine($"{dest}, {src}");

            stringBuilder.Append(instructionEncodings[opCode] + " ");
            stringBuilder.Append($"{dest}, {src}\n");
        }
        Console.WriteLine(stringBuilder.ToString());
    }

    // TODO: This way of doing things is redundant. We do a pass on the W or MOD just to get a range
    // Then we do it again. Move main logic of RegMemToFromRegister here and go from there.
    // This method is also redundant in the face of the main loop where we're doing switch on the OpCodes
    private static Range GetOpRange(int opCode, ref byte[] buffer, int startPos)
    {
        var opRange = new Range();
        switch (opCode)
        {
            case 0b1011:
            {
                var w = buffer[startPos] >> 3 & 1;
                opRange = w == 0
                    ? startPos..(startPos + 2)
                    : startPos..(startPos + 3);
            }
                break;
            case 0b100010:
            {
                var mod = buffer[startPos + 1] >> 6 & 7;
                switch (mod)
                {
                    case 0b00: // 2 (cur + 2)
                        opRange = startPos..(startPos + 2);
                        break;
                    case 0b01: // 3 (cur + 3)
                        opRange = startPos..(startPos + 3);
                        break;
                    case 0b10: // 4
                        opRange = startPos..(startPos + 4);
                        break;
                    case 0b11: // 2
                        opRange = startPos..(startPos + 2);
                        break;
                }
            }

                break;
        }

        return opRange;
    }

    // TODO: Why get a sub-array of the instruction bytes if you're no using the range?
    private static (string dest, string src) RegMemToFromRegister(byte[] instrBytes)
    {
        string dest = string.Empty;
        string src = string.Empty;

        var D = instrBytes[0] >> 1 & 3;
        var W = instrBytes[0] >> 0 & 1;
        var Mod = instrBytes[1] >> 6 & 7;
        var Reg = instrBytes[1] >> 3 & 7;
        var Rm = instrBytes[1] >> 0 & 7;

        // Memory Mode, no displacement, unless if R/M = 110, then 16 bit displacement
        // NASM will actually encode this as MOD - 01 or 10
        if (Mod == 0b00)
        {
            var decodedRm = $"[{rmFieldEffectiveAddressEncoding[Rm].First()}]";
            var decodedReg = D == 0
                ? regFieldMemoryModeEncoding[Reg][W]
                : regFieldMemoryModeEncoding[Rm][W];

            (dest, src) = D == 0
                ? (decodedRm, decodedReg)
                : (decodedReg, decodedRm);
        }

        // Memory Mode - 8 bit displacement
        if (Mod == 0b01)
        {
            string decodedRm = string.Empty;

            // This is an edge case for the [bp].
            if (Rm == 0b110 && instrBytes[2] == 0)
                decodedRm = $"[{rmFieldEffectiveAddressEncoding[Rm].First()}]";
            else
                decodedRm = $"[{rmFieldEffectiveAddressEncoding[Rm].First()} + {instrBytes[2]}]";

            var decodedReg = regFieldMemoryModeEncoding[Reg][W];

            (dest, src) = D == 1
                ? (decodedReg, decodedRm)
                : (decodedRm, decodedReg);
        }

        // Memory Mode - 16 bit displacement
        if (Mod == 0b10)
        {
            var data = (short)((instrBytes[3] << 8) | (instrBytes[2] << 0));
            src = $"[{rmFieldEffectiveAddressEncoding[Rm].First()} + {data}]";

            dest = D == 0
                ? regFieldMemoryModeEncoding[Rm][W]
                : regFieldMemoryModeEncoding[Reg][W];
        }

        // Register Mode without displacement
        if (Mod == 0b11)
        {
            (dest, src) = D == 0
                ? (regFieldMemoryModeEncoding[Rm][W], regFieldMemoryModeEncoding[Reg][W])
                : (regFieldMemoryModeEncoding[Reg][W], regFieldMemoryModeEncoding[Rm][W]);
        }

        return (dest, src);
    }

    // TODO: Same as the above method. Why get the range if it's not used. There's no bounds checking
    private static (string dest, string src) ImmediateToRegister(byte[] instrBytes)
    {
        var W = instrBytes[0] >> 3 & 1;
        var Reg = instrBytes[0] >> 0 & 7;
        int Data;

        if (W == 0)
            Data = (sbyte)instrBytes[1];
        else // Little endian
            Data = (short)((instrBytes[2] << 8) | (instrBytes[1] << 0));

        return (
            dest: regFieldMemoryModeEncoding[Reg][W],
            src: Data.ToString());
    }

    static int GetOpCode(int opByte)
    {
        // check if we're doing immediate-to-register first
        var opCode = opByte >> 4;
        if (instructionEncodings.ContainsKey(opCode))
            return opCode;

        // check if we're doing register/memory to/from register
        opCode = opByte >> 2;
        if (instructionEncodings.ContainsKey(opCode))
            return opCode;

        // TODO: Will blow up if opCode is not found
        return 0;
    }
}