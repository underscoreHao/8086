using System.Text;

internal class Program
{
	private enum InstructionType
	{
		MOV_IMM_TO_REG_MEM = 0b1011,
		MOV_REG_MEM_TO_REG_MEM = 0b100010,
	};

	private static readonly Dictionary<int, string[]> regFieldMemoryModeEncoding = new()
	{
		// REG   W = 0, W = 1
		{ 0b000, ["al", "ax"] },
		{ 0b001, ["cl", "cx"] },
		{ 0b010, ["dl", "dx"] },
		{ 0b011, ["bl", "bx"] },
		{ 0b100, ["ah", "sp"] },
		{ 0b101, ["ch", "bp"] },
		{ 0b110, ["dh", "si"] },
		{ 0b111, ["bh", "di"] },
	};

	private static readonly string[] rmFieldEffectiveAddressEncoding =
	[
		"bx + si",
		"bx + di",
		"bp + si",
		"bp + di",
		"si",
		"di",
		"bp", // Direct 16-bit displacement
		"bx"
	];

	private static void Main(string[] args)
	{
		Console.WriteLine("8086 v0.1");
		Console.WriteLine();

		using var stdin = Console.OpenStandardInput();
		using var stdout = Console.OpenStandardOutput();

		byte[] buffer = new byte[256];
		int bufferSize = stdin.Read(buffer, 0, buffer.Length);

#if DEBUG

		for (int i = 0; i < bufferSize; i += 2)
			Console.WriteLine(Convert.ToString(buffer[i], 2) + ", " + Convert.ToString(buffer[i + i], 2));

		Console.WriteLine();

#endif

		var stringBuilder = new StringBuilder();
		stringBuilder.AppendLine("bits 16");
		stringBuilder.AppendLine();

		int idx = 0;
		while (idx < bufferSize)
		{
			var opByte = buffer[idx++];

			if (opByte >> 4 == (int)InstructionType.MOV_IMM_TO_REG_MEM)
			{
				var w = opByte >> 3 & 1;
				var reg = opByte & 7;
				// Little endian
				short data = w == 0
					? (sbyte)buffer[idx++]
					: (short)(buffer[idx++] | (buffer[idx++] << 8));

				stringBuilder.Append($"mov {regFieldMemoryModeEncoding[reg][w]}, {data}\n");
			}
			else if (opByte >> 2 == (int)InstructionType.MOV_REG_MEM_TO_REG_MEM)
			{
				var d = (opByte & 2) >> 1;
				var w = opByte & 1;

				var byte2 = buffer[idx++];
				var mod = byte2 >> 6;
				var reg = byte2 >> 3 & 7;
				var rm = byte2 & 7;

				if (mod == 0b11)
				{
					stringBuilder.Append(d == 1
						? $"mov {regFieldMemoryModeEncoding[reg][w]}, {regFieldMemoryModeEncoding[rm][w]}\n"
						: $"mov {regFieldMemoryModeEncoding[rm][w]}, {regFieldMemoryModeEncoding[reg][w]}\n");
				}
				else
				{
					var dispOrDirectAddr = mod switch
					{
						0b00 when rm == 0b110 => (short)(buffer[idx++] | (buffer[idx++] << 8)),
						0b01 => (sbyte)buffer[idx++],
						0b10 => (short)(buffer[idx++] | (buffer[idx++] << 8)),
						_ => 0
					};

					string addressCalc;
					if (mod == 0b00 && rm == 0b110)
					{
						addressCalc = $"[{dispOrDirectAddr}]";
					}
					else
					{
						addressCalc = dispOrDirectAddr switch
						{
							0 => $"[{rmFieldEffectiveAddressEncoding[rm]}]",
							> 0 => $"[{rmFieldEffectiveAddressEncoding[rm]} + {dispOrDirectAddr}]",
							_ => $"[{rmFieldEffectiveAddressEncoding[rm]} - {-1 * dispOrDirectAddr}]"
						};
					}

					stringBuilder.Append(d == 1
						? $"mov {regFieldMemoryModeEncoding[reg][w]}, {addressCalc}\n"
						: $"mov {addressCalc}, {regFieldMemoryModeEncoding[reg][w]}\n");
				}
			}
		}

		Console.WriteLine(stringBuilder.ToString());
	}
}
