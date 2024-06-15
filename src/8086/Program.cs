using System.Text;

Console.WriteLine("8086 v0.1");
Console.WriteLine();

using var stdin = Console.OpenStandardInput();
using var stdout = Console.OpenStandardOutput();

byte[] buffer = new byte[256];
int bufferSize = stdin.Read(buffer, 0, buffer.Length);

// int bytes;
// while ((bytes = stdin.Read(buffer, 0, buffer.Length)) > 0)
// {
// 	// Process bytes from buffer here
// 	// If the data is larger than the buffer, this loop will execute multiple times
// }

#if DEBUG

for (int i = 0; i < bufferSize; i += 2)
	Console.WriteLine(Convert.ToString(buffer[i], 2) + ", " + Convert.ToString(buffer[i + i], 2));

Console.WriteLine();

#endif

#region OPCODE DECODER

var instructionEncodings = new Dictionary<int, string>()
{
	{ 0b100010, "mov" }
};

var modFieldEncodings = new int[] { 0b00, 0b01, 0b10, 0b11 };

var regFieldEncodings = new Dictionary<int, string[]>()
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

var stringBuilder = new StringBuilder();
stringBuilder.AppendLine("bits 16");
stringBuilder.AppendLine();

for (int i = 0; i < bufferSize; i += 2)
{
	int opCode = buffer[i] >> 2;
	int d = buffer[i] >> 1 & 2;
	int w = buffer[i] & 1;

	int mod = buffer[i + 1] >> 6;
	int reg = buffer[i + 1] >> 3 & 7;
	int rm = buffer[i + 1] & 7;

	// TODO: This will fail horribly if the opCode is not `mov`
	stringBuilder.Append(instructionEncodings[opCode] + " ");

	string src = d == 0 ? regFieldEncodings[reg][w] : regFieldEncodings[rm][w];
	string dest = d == 0 ? regFieldEncodings[rm][w] : regFieldEncodings[reg][w];

	stringBuilder.Append(dest + ", " + src);
	stringBuilder.AppendLine();
}

Console.WriteLine(stringBuilder.ToString());

#endregion
