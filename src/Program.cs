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

		var stringBuilder = new StringBuilder();
		stringBuilder.AppendLine("bits 16\n");

		var instr = new Instruction();
		string src = string.Empty;
		string dest = string.Empty;

		int i = 0;
		while (i < bufferSize)
		{
			var opCode = GetOpCode(buffer[i]);
			instr.OpCode = opCode;
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
					// TODO: Extract this into a function
					// ======================================
					instr.D = buffer[i] >> 1 & 3;
					instr.W = buffer[i] >> 0 & 1;


					instr.Mod = buffer[i + 1] >> 6 & 7;
					instr.Reg = buffer[i + 1] >> 3 & 7;
					instr.Rm = buffer[i + 1] >> 0 & 7;
					// ======================================

					if (instr.Mod == 0b00)
					{
						var address = $"[{regFieldEffectiveAddressEncodings[instr.Rm].First()}]";
						var reg = instr.D == 0
							? regFieldMemoryModeEncodings[instr.Rm][instr.W]
							: regFieldMemoryModeEncodings[instr.Reg][instr.W];

						if (instr.D == 0)
						{
							dest = address;
							src = regFieldMemoryModeEncodings[instr.Reg][instr.W];
						}
						else
						{
							src = address;
							dest = reg;
						}
					}

					if (instr.Mod == 0b01)
					{
						string reg = string.Empty;
						if (instr.Rm == 0b110 && buffer[i + 2] == 0)
						{
							reg = $"[{regFieldEffectiveAddressEncodings[instr.Rm].First()}]";
						}
						else
						{
							reg = $"[{regFieldEffectiveAddressEncodings[instr.Rm].First()} + {buffer[i + 2]}]";
						}

						var address = regFieldMemoryModeEncodings[instr.Reg][instr.W];
						if (instr.D == 1)
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

						dest = instr.D == 0
							? regFieldMemoryModeEncodings[instr.Rm][instr.W]
							: regFieldMemoryModeEncodings[instr.Reg][instr.W];
					}

					if (instr.Mod == 0b11)
					{
						src = instr.D == 0
									? regFieldMemoryModeEncodings[instr.Reg][instr.W]
									: regFieldMemoryModeEncodings[instr.Rm][instr.W];

						dest = instr.D == 0
							? regFieldMemoryModeEncodings[instr.Rm][instr.W]
							: regFieldMemoryModeEncodings[instr.Reg][instr.W];
					}

					break;
			}

			i = opRange.End.Value;

			Console.WriteLine($"{dest}, {src}");

			stringBuilder.Append(instructionEncodings[opCode] + " ");
			stringBuilder.Append($"{dest}, {src}\n");
		}
		Console.WriteLine(stringBuilder.ToString());
	}

	private static Range GetOpRange(int opCode, ref byte[] buffer, int startPos)
	{
		var opRange = new Range();
		switch (opCode)
		{
			case 0b1011:
				{
					var w = buffer[startPos] >> 3 & 1;
					opRange = w == 0
						? new Range(startPos, startPos + 2)
						: new Range(startPos, startPos + 3);
				}
				break;
			case 0b100010:
				{
					var mod = buffer[startPos + 1] >> 6 & 7;
					switch (mod)
					{
						case 0b00: // 2 (cur + 2)
							opRange = new Range(startPos, startPos + 2);
							break;
						case 0b01: // 3 (cur + 3)
							opRange = new Range(startPos, startPos + 3);
							break;
						case 0b10: // 4
							opRange = new Range(startPos, startPos + 4);
							break;
						case 0b11: // 2
							opRange = new Range(startPos, startPos + 2);
							break;
					}
				}

				break;
		}

		return opRange;
	}

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
			dest: regFieldMemoryModeEncodings[Reg][W],
			src: Data.ToString());
	}

	private static Instruction BuildInstruction(int opCode, byte[] buffer)
	{
		return new Instruction
		{
			OpCode = opCode,
			W = buffer[0] >> 3 & 1
		};
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

public struct Instruction
{
	public int OpCode { get; set; }
	public int D { get; set; }
	public int W { get; set; }
	public int Mod { get; set; }
	public int Reg { get; set; }
	public int Rm { get; set; }
	public int Data { get; set; }
	public int[] DataSet { get; set; }
}
