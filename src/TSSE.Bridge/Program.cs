using System.Text;
using TSSE.Bridge.Protocol;

Console.InputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

while (true)
{
    BoundedLine? input = await ReadLineAsync(Console.In, BridgeProtocol.MaximumRequestCharacters);
    if (input is null)
        break;

    string response = input.TooLong
        ? BridgeProtocol.Error(string.Empty, "request_size_invalid")
        : BridgeProtocol.HandleLine(input.Value);

    await Console.Out.WriteLineAsync(response);
    await Console.Out.FlushAsync();
}

static async Task<BoundedLine?> ReadLineAsync(TextReader reader, int maximumCharacters)
{
    var buffer = new StringBuilder(Math.Min(maximumCharacters, 256));
    char[] oneCharacter = new char[1];
    bool tooLong = false;

    while (await reader.ReadAsync(oneCharacter.AsMemory()).ConfigureAwait(false) != 0)
    {
        char current = oneCharacter[0];
        if (current == '\n')
            return new BoundedLine(buffer.ToString().TrimEnd('\r'), tooLong);

        if (buffer.Length < maximumCharacters)
            buffer.Append(current);
        else
            tooLong = true;
    }

    return buffer.Length == 0 && !tooLong
        ? null
        : new BoundedLine(buffer.ToString().TrimEnd('\r'), tooLong);
}

internal sealed record BoundedLine(string Value, bool TooLong);
