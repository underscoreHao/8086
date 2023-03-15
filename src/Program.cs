using System.Text;

class Program
{
	private static readonly Dictionary<int, string> instructionEncodings = new()
	{
		{ 0b100010, "mov" },
		{ 0b1011, "mov" }
	};
	private static int[] modFieldEncodings = new int[] { 0b00, 0b01, 0b10, 0b11 };

	private static readonly Dictionary<int, string[]> regFieldMemoryModeEncodings = new()
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

	private static readonly Dictionary<int, string[]> regFieldEffectiveAddressEncodings = new()
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

#if DEBUG

		for (int j = 0; j < bufferSize - 1; j += 2)
			Console.WriteLine(Convert.ToString(buffer[j], 2) + ", " + Convert.ToString(buffer[j + 1], 2));

		Console.WriteLine();

#endif

		var stringBuilder = new StringBuilder();
		stringBuilder.AppendLine("bits 16\n");

		var instr = new Instruction();
		string src = string.Empty;
		string dest = string.Empty;

		int i = 0;
		while (i < bufferSize)
		{
			//Console.WriteLine(Convert.ToString(buffer[i], 2));

			instr.OpCode = GetOpCode(buffer[i]);

			switch (instr.OpCode)
			{
				case 0b1011:
					instr.W_Flag = buffer[i] >> 3 & 1;
					instr.Reg = buffer[i] >> 0 & 7;
					if (instr.W_Flag == 0)
					{
						instr.Data = (sbyte)buffer[i + 1];
						i += 2;
					}
					else
					{
						instr.Data = (short)((buffer[i + 2] << 8) | (buffer[i + 1] << 0));
						i += 3; // skip next two bytes as they've been read
					}
					dest = regFieldMemoryModeEncodings[instr.Reg][instr.W_Flag];
					src = instr.Data.ToString();

					break;
				case 0b100010:
					instr.D_Flag = buffer[i] >> 1 & 3;
					instr.W_Flag = buffer[i] >> 0 & 1;


					instr.Mod = buffer[i + 1] >> 6 & 7;
					instr.Reg = buffer[i + 1] >> 3 & 7;
					instr.Rm = buffer[i + 1] >> 0 & 7;

					if (instr.Mod == 0b00)
					{
						var address = $"[{regFieldEffectiveAddressEncodings[instr.Rm].First()}]";
						var reg = instr.D_Flag == 0
							? regFieldMemoryModeEncodings[instr.Rm][instr.W_Flag]
							: regFieldMemoryModeEncodings[instr.Reg][instr.W_Flag];

						if (instr.D_Flag == 0)
						{
							dest = address;
							src = regFieldMemoryModeEncodings[instr.Reg][instr.W_Flag];
						}
						else
						{
							src = address;
							dest = reg;
						}

						i += 2;
					}

					if (instr.Mod == 0b01)
					{
						string reg = string.Empty;
						if (instr.Rm == 0b110 && buffer[i + 2] == 0)
						{
							// read 16 bit displacement
							i += 3;
							reg = $"[{regFieldEffectiveAddressEncodings[instr.Rm].First()}]";
						}
						else
						{
							reg = $"[{regFieldEffectiveAddressEncodings[instr.Rm].First()} + {buffer[i + 2]}]";
							i += 3;
						}

						var address = regFieldMemoryModeEncodings[instr.Reg][instr.W_Flag];
						if (instr.D_Flag == 1)
						{
							dest = address;
							src = reg;
						}
						else
						{
							src = address;
							dest = reg;
						}
					}

					if (instr.Mod == 0b10)
					{
						// read 16 bit displacement
						var data = (short)((buffer[i + 3] << 8) | (buffer[i + 2] << 0));
						src = $"[{regFieldEffectiveAddressEncodings[instr.Rm].First()} + {data}]";

						dest = instr.D_Flag == 0
							? regFieldMemoryModeEncodings[instr.Rm][instr.W_Flag]
							: regFieldMemoryModeEncodings[instr.Reg][instr.W_Flag];

						i += 4;
					}

					if (instr.Mod == 0b11)
					{
						src = instr.D_Flag == 0
									? regFieldMemoryModeEncodings[instr.Reg][instr.W_Flag]
									: regFieldMemoryModeEncodings[instr.Rm][instr.W_Flag];

						dest = instr.D_Flag == 0
							? regFieldMemoryModeEncodings[instr.Rm][instr.W_Flag]
							: regFieldMemoryModeEncodings[instr.Reg][instr.W_Flag];

						i += 2;
					}

					break;
			}

			Console.WriteLine($"{dest}, {src}");

			stringBuilder.Append(instructionEncodings[instr.OpCode] + " ");
			stringBuilder.Append($"{dest}, {src}\n");
		}
		Console.WriteLine(stringBuilder.ToString());
	}

	static int GetOpCode(int byte1)
	{
		// check if we're doing immediate-to-register first
		var opCode = byte1 >> 4;
		if (instructionEncodings.ContainsKey(opCode))
			return opCode;

		// check if we're doing register/memory to/from register
		opCode = byte1 >> 2;
		if (instructionEncodings.ContainsKey(opCode))
			return opCode;

		// TODO: Will blow up if opCode is not found
		return 0;
	}
}

public struct Instruction
{
	public int OpCode { get; set; }
	public int D_Flag { get; set; }
	public int W_Flag { get; set; }
	public int Mod { get; set; }
	public int Reg { get; set; }
	public int Rm { get; set; }
	public int Data { get; set; }
}
