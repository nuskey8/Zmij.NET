using ZimijNet;

double value = 1.234567890123456;
Console.WriteLine(Zimij.ToString(value));

Span<byte> buffer = stackalloc byte[20];
Zimij.TryWrite(value, buffer, out int bytesWritten);

ZimijDecimal d = Zimij.ToDecimal(value);
Console.WriteLine(d.Significand);
Console.WriteLine(d.Exponent);
Console.WriteLine(d.IsNegative);
