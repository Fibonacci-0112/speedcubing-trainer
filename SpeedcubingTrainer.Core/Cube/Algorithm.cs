using System.Collections;
using System.Text;

namespace SpeedcubingTrainer.Core.Cube;

/// <summary>Thrown when an algorithm string cannot be parsed.</summary>
public sealed class AlgorithmParseException(string message, int position) : FormatException($"{message} (at position {position})")
{
    public int Position { get; } = position;
}

/// <summary>An immutable sequence of moves.</summary>
public sealed class Algorithm : IReadOnlyList<Move>, IEquatable<Algorithm>
{
    public static readonly Algorithm Empty = new([]);

    private readonly Move[] _moves;

    public Algorithm(IEnumerable<Move> moves)
    {
        _moves = moves.ToArray();
    }

    private Algorithm(Move[] moves, bool _)
    {
        _moves = moves;
    }

    public int Count => _moves.Length;

    public Move this[int index] => _moves[index];

    /// <summary>Number of moves in half-turn metric: rotations count 0, everything else 1.</summary>
    public int HtmLength => _moves.Count(m => !m.IsRotation);

    public ReadOnlySpan<Move> Moves => _moves;

    public Algorithm Inverse()
    {
        var result = new Move[_moves.Length];
        for (var i = 0; i < _moves.Length; i++)
        {
            result[i] = _moves[_moves.Length - 1 - i].Inverse;
        }
        return new Algorithm(result, true);
    }

    public Algorithm Mirror(MirrorPlane plane = MirrorPlane.LeftRight)
    {
        var result = new Move[_moves.Length];
        for (var i = 0; i < _moves.Length; i++)
        {
            result[i] = _moves[i].Mirror(plane);
        }
        return new Algorithm(result, true);
    }

    public Algorithm Concat(Algorithm other)
    {
        var result = new Move[_moves.Length + other._moves.Length];
        _moves.CopyTo(result, 0);
        other._moves.CopyTo(result, _moves.Length);
        return new Algorithm(result, true);
    }

    public Algorithm Append(Move move)
    {
        var result = new Move[_moves.Length + 1];
        _moves.CopyTo(result, 0);
        result[^1] = move;
        return new Algorithm(result, true);
    }

    public static Algorithm operator +(Algorithm a, Algorithm b) => a.Concat(b);

    /// <summary>
    /// Merges consecutive moves on the same target (R R' cancels, R R becomes R2) so that
    /// generated sequences such as setups read naturally.
    /// </summary>
    public Algorithm Simplify()
    {
        var stack = new List<Move>(_moves.Length);
        foreach (var move in _moves)
        {
            if (stack.Count > 0 && stack[^1].Target == move.Target)
            {
                var total = ((int)stack[^1].Turn + (int)move.Turn) % 4;
                stack.RemoveAt(stack.Count - 1);
                if (total != 0)
                {
                    stack.Add(new Move(move.Target, (Turn)total));
                }
            }
            else
            {
                stack.Add(move);
            }
        }
        return new Algorithm([.. stack], true);
    }

    public static Algorithm Parse(string text)
    {
        return TryParse(text, out var algorithm, out var error, out var position)
            ? algorithm
            : throw new AlgorithmParseException(error, position);
    }

    public static bool TryParse(string text, out Algorithm algorithm) =>
        TryParse(text, out algorithm, out _, out _);

    public static bool TryParse(string text, out Algorithm algorithm, out string error, out int errorPosition)
    {
        var parser = new Parser(text);
        if (parser.TryParseAll(out var moves, out error, out errorPosition))
        {
            algorithm = new Algorithm(moves.ToArray(), true);
            return true;
        }
        algorithm = Empty;
        return false;
    }

    public override string ToString() => ToString(NotationStyle.Wide);

    public string ToString(NotationStyle style)
    {
        var sb = new StringBuilder(_moves.Length * 3);
        for (var i = 0; i < _moves.Length; i++)
        {
            if (i > 0)
            {
                sb.Append(' ');
            }
            sb.Append(_moves[i].ToString(style));
        }
        return sb.ToString();
    }

    public bool Equals(Algorithm? other) => other is not null && _moves.AsSpan().SequenceEqual(other._moves);

    public override bool Equals(object? obj) => Equals(obj as Algorithm);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var move in _moves)
        {
            hash.Add(move);
        }
        return hash.ToHashCode();
    }

    public IEnumerator<Move> GetEnumerator() => ((IEnumerable<Move>)_moves).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Recursive-descent parser for move sequences: tokens separated by whitespace, groups in
    /// parentheses or square brackets with an optional repeat count, and comments introduced by //.
    /// </summary>
    private ref struct Parser(string text)
    {
        private readonly string _text = text;
        private int _pos;

        public bool TryParseAll(out List<Move> moves, out string error, out int errorPosition)
        {
            moves = new List<Move>();
            error = string.Empty;
            errorPosition = -1;
            try
            {
                ParseSequence(moves, closing: null, openPosition: 0);
                return true;
            }
            catch (AlgorithmParseException ex)
            {
                error = ex.Message;
                errorPosition = ex.Position;
                return false;
            }
        }

        private void ParseSequence(List<Move> output, char? closing, int openPosition)
        {
            while (true)
            {
                SkipTrivia();
                if (_pos >= _text.Length)
                {
                    if (closing is not null)
                    {
                        throw new AlgorithmParseException($"Missing closing '{closing}'", openPosition);
                    }
                    return;
                }

                var c = _text[_pos];
                if (c is ')' or ']')
                {
                    if (closing is null || c != closing)
                    {
                        throw new AlgorithmParseException($"Unexpected '{c}'", _pos);
                    }
                    _pos++;
                    return;
                }

                if (c is '(' or '[')
                {
                    var start = _pos;
                    _pos++;
                    var inner = new List<Move>();
                    ParseSequence(inner, c == '(' ? ')' : ']', start);
                    var repeat = ParseRepeatSuffix();
                    for (var i = 0; i < repeat; i++)
                    {
                        output.AddRange(inner);
                    }
                    continue;
                }

                output.Add(ParseMove());
            }
        }

        /// <summary>Optional repeat after a group: "3", "*3", "x3", or a trailing ' to invert.</summary>
        private int ParseRepeatSuffix()
        {
            SkipSpaces();
            if (_pos < _text.Length && (_text[_pos] == '*' || _text[_pos] == 'x') && _pos + 1 < _text.Length && char.IsDigit(_text[_pos + 1]))
            {
                _pos++;
            }
            var start = _pos;
            while (_pos < _text.Length && char.IsDigit(_text[_pos]))
            {
                _pos++;
            }
            if (_pos == start)
            {
                return 1;
            }
            var count = int.Parse(_text.AsSpan(start, _pos - start));
            if (count > 100)
            {
                throw new AlgorithmParseException("Repeat count is too large", start);
            }
            return count;
        }

        private Move ParseMove()
        {
            var start = _pos;
            var c = _text[_pos];
            MoveTarget target;
            switch (c)
            {
                case 'U': target = MoveTarget.U; break;
                case 'R': target = MoveTarget.R; break;
                case 'F': target = MoveTarget.F; break;
                case 'D': target = MoveTarget.D; break;
                case 'L': target = MoveTarget.L; break;
                case 'B': target = MoveTarget.B; break;
                case 'u': target = MoveTarget.Uw; break;
                case 'r': target = MoveTarget.Rw; break;
                case 'f': target = MoveTarget.Fw; break;
                case 'd': target = MoveTarget.Dw; break;
                case 'l': target = MoveTarget.Lw; break;
                case 'b': target = MoveTarget.Bw; break;
                case 'M': target = MoveTarget.M; break;
                case 'E': target = MoveTarget.E; break;
                case 'S': target = MoveTarget.S; break;
                case 'x': target = MoveTarget.X; break;
                case 'y': target = MoveTarget.Y; break;
                case 'z': target = MoveTarget.Z; break;
                default:
                    throw new AlgorithmParseException($"Unexpected character '{c}'", _pos);
            }
            _pos++;

            if (_pos < _text.Length && _text[_pos] == 'w')
            {
                if (target > MoveTarget.B)
                {
                    throw new AlgorithmParseException("'w' is only valid after a face letter", _pos);
                }
                target = (MoveTarget)((int)target + 6);
                _pos++;
            }

            var quarterTurns = 1;
            var sawDigit = false;
            var sawPrime = false;
            while (_pos < _text.Length)
            {
                var s = _text[_pos];
                if (s == '2' && !sawDigit)
                {
                    quarterTurns = 2;
                    sawDigit = true;
                    _pos++;
                }
                else if (s == '3' && !sawDigit)
                {
                    quarterTurns = 3;
                    sawDigit = true;
                    _pos++;
                }
                else if ((s == '\'' || s == '’' || s == '`') && !sawPrime)
                {
                    sawPrime = true;
                    _pos++;
                }
                else
                {
                    break;
                }
            }

            if (_pos < _text.Length && !char.IsWhiteSpace(_text[_pos]) && _text[_pos] is not ('(' or ')' or '[' or ']' or '/' or ','))
            {
                throw new AlgorithmParseException($"Invalid move '{_text[start.._pos]}{_text[_pos]}'", start);
            }

            if (sawPrime)
            {
                quarterTurns = 4 - quarterTurns;
            }
            return new Move(target, (Turn)quarterTurns);
        }

        private void SkipSpaces()
        {
            while (_pos < _text.Length && (_text[_pos] == ' ' || _text[_pos] == '\t'))
            {
                _pos++;
            }
        }

        private void SkipTrivia()
        {
            while (_pos < _text.Length)
            {
                var c = _text[_pos];
                if (char.IsWhiteSpace(c) || c == ',')
                {
                    _pos++;
                }
                else if (c == '/' && _pos + 1 < _text.Length && _text[_pos + 1] == '/')
                {
                    while (_pos < _text.Length && _text[_pos] != '\n')
                    {
                        _pos++;
                    }
                }
                else
                {
                    return;
                }
            }
        }
    }
}
