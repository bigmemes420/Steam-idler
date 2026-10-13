const int TableSize = 32, Multiplier = 16807, Modulus = 2147483647, Quotient = 127773, Remainder = 2836;
var seed = 129634044 >= 0 ? -129634044 : 129634044;
var current = 0;
var table = new int[TableSize];

int Next()
{
    if (seed <= 0 || current == 0)
    {
        seed = -seed < 1 ? 1 : -seed;
        for (var index = TableSize + 7; index >= 0; index--)
        {
            var quotient = seed / Quotient;
            seed = (Multiplier * (seed - (quotient * Quotient))) - (Remainder * quotient);
            if (seed < 0)
            {
                seed += Modulus;
            }

            if (index < TableSize)
            {
                table[index] = seed;
            }
        }

        current = table[0];
    }

    var nextQuotient = seed / Quotient;
    seed = (Multiplier * (seed - (nextQuotient * Quotient))) - (Remainder * nextQuotient);
    if (seed < 0)
    {
        seed += Modulus;
    }

    var slot = current / (1 + ((Modulus - 1) / TableSize));
    if (slot is < 0 or >= TableSize)
    {
        slot = slot % TableSize & int.MaxValue;
    }

    current = table[slot];
    table[slot] = seed;
    return current;
}

int NextRange(int low, int high)
{
    var span = high - low + 1;
    var limit = int.MaxValue - (int)(((uint)int.MaxValue + 1) % (uint)span);
    int sample;
    do
    {
        sample = Next();
    }
    while (sample > limit);

    return low + (sample % span);
}

var encrypted = File.ReadAllBytes(@"C:\Program Files (x86)\Steam\userdata\129634044\config\licensecache");
var output = new byte[encrypted.Length];
for (var index = 0; index < encrypted.Length; index++)
{
    output[index] = (byte)(encrypted[index] ^ NextRange(32, 126));
}

File.WriteAllBytes(@"_scratch\decrypted-cs.bin", output);
Console.WriteLine("wrote " + output.Length);
var body = output.AsSpan(0, output.Length - 4);
var offset = 0;
var count = 0;
while (offset < body.Length)
{
    var at = offset;
    var key = ReadVarint(body, ref offset);
    var field = (int)(key >> 3);
    var wire = (int)(key & 7);
    if (wire is not (0 or 1 or 2 or 5))
    {
        Console.WriteLine($"bad wire {wire} field {field} at {at} after {count} licenses, head {Convert.ToHexString(body.Slice(Math.Max(0, at - 8), Math.Min(24, body.Length - Math.Max(0, at - 8))))}");
        return;
    }

    if (field == 2 && wire == 2)
    {
        var length = (int)ReadVarint(body, ref offset);
        var license = body.Slice(offset, length);
        offset += length;
        var inner = 0;
        try
        {
            while (inner < license.Length)
            {
                var innerAt = inner;
                var innerKey = ReadVarint(license, ref inner);
                var innerField = (int)(innerKey >> 3);
                var innerWire = (int)(innerKey & 7);
                if (innerWire is not (0 or 1 or 2 or 5))
                {
                    Console.WriteLine($"inner bad wire {innerWire} field {innerField} license {count} at {innerAt} len {length} hex {Convert.ToHexString(license)}");
                    return;
                }

                if (innerField == 1 && innerWire == 0)
                {
                    _ = ReadVarint(license, ref inner);
                }
                else
                {
                    Skip(license, ref inner, innerWire);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"license {count} len {length} failed {ex.Message} hex {Convert.ToHexString(license)}");
            return;
        }

        count++;
    }
    else
    {
        Skip(body, ref offset, wire);
    }
}

Console.WriteLine("licenses " + count);

static ulong ReadVarint(ReadOnlySpan<byte> data, ref int offset)
{
    ulong value = 0;
    var shift = 0;
    while (offset < data.Length)
    {
        var current = data[offset++];
        value |= (ulong)(current & 0x7F) << shift;
        if (current < 128)
        {
            return value;
        }

        shift += 7;
    }

    throw new InvalidDataException("trunc");
}

static void Skip(ReadOnlySpan<byte> data, ref int offset, int wire)
{
    switch (wire)
    {
        case 0: _ = ReadVarint(data, ref offset); break;
        case 1: offset += 8; break;
        case 2: offset += (int)ReadVarint(data, ref offset); break;
        case 5: offset += 4; break;
    }
}
