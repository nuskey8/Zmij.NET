using ZmijNet;

double value = 1.234567890123456;
Console.WriteLine(Zmij.ToString(value));

Span<byte> buffer = stackalloc byte[20];
Zmij.TryWrite(value, buffer, out int bytesWritten);

ZmijDecimal d = Zmij.ToDecimal(value);
Console.WriteLine(d.Significand);
Console.WriteLine(d.Exponent);
Console.WriteLine(d.IsNegative);
