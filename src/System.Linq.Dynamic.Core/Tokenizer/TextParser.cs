﻿using System.Collections.Generic;
using System.Globalization;
using System.Linq.Dynamic.Core.Exceptions;

namespace System.Linq.Dynamic.Core.Tokenizer;

/// <summary>
/// TextParser which can be used to parse a text into tokens.
/// </summary>
public class TextParser
{
    private const char DefaultNumberDecimalSeparator = '.';
    private static readonly char[] EscapeCharacters = ['\\', 'a', 'b', 'f', 'n', 'r', 't', 'v'];

    private readonly char _numberDecimalSeparator;
    private readonly string _text;
    private readonly int _textLen;
    private readonly ParsingConfig _parsingConfig;

    // These aliases simplify the "Where"-clause and make it more human-readable.
    private readonly Dictionary<string, TokenId> _predefinedOperatorAliases;

    private int _textPos;
    private char _ch;

    /// <summary>
    /// The current parsed <see cref="Token"/>.
    /// </summary>
    public Token CurrentToken;

    /// <summary>
    /// Constructor for TextParser
    /// </summary>
    /// <param name="config"></param>
    /// <param name="text"></param>
    public TextParser(ParsingConfig config, string text)
    {
        _parsingConfig = config;

        _predefinedOperatorAliases = new(config.IsCaseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase)
        {
            { "eq", TokenId.Equal },
            { "equal", TokenId.Equal },
            { "ne", TokenId.ExclamationEqual },
            { "notequal", TokenId.ExclamationEqual },
            { "neq", TokenId.ExclamationEqual },
            { "lt", TokenId.LessThan },
            { "LessThan", TokenId.LessThan },
            { "le", TokenId.LessThanEqual },
            { "LessThanEqual", TokenId.LessThanEqual },
            { "gt", TokenId.GreaterThan },
            { "GreaterThan", TokenId.GreaterThan },
            { "ge", TokenId.GreaterThanEqual },
            { "GreaterThanEqual", TokenId.GreaterThanEqual },
            { "and", TokenId.DoubleAmpersand },
            { "AndAlso", TokenId.DoubleAmpersand },
            { "or", TokenId.DoubleBar },
            { "OrElse", TokenId.DoubleBar },
            { "not", TokenId.Exclamation },
            { "mod", TokenId.Percent }
        };

        _numberDecimalSeparator = config.NumberParseCulture?.NumberFormat.NumberDecimalSeparator[0] ?? DefaultNumberDecimalSeparator;

        _text = text;
        _textLen = _text.Length;

        SetTextPos(0);
        NextToken();
    }

    /// <summary>
    /// This method is used to clone the current <see cref="TextParser"/>.
    /// </summary>
    /// <returns>Cloned <see cref="TextParser"/></returns>
    public TextParser Clone()
    {
        var clone = new TextParser(_parsingConfig, _text);
        clone.SetTextPos(_textPos);

        return clone;
    }

    /// <summary>
    /// Peek the next character.
    /// </summary>
    /// <returns>The next character, or \0 if end of string.</returns>
    public char PeekNextChar()
    {
        return _textPos + 1 < _textLen ? _text[_textPos + 1] : '\0';
    }

    /// <summary>
    /// Go to the next token.
    /// </summary>
    public void NextToken()
    {
        while (char.IsWhiteSpace(_ch))
        {
            NextChar();
        }

        TokenId tokenId;
        var tokenPos = _textPos;

        switch (_ch)
        {
            case '!':
                NextChar();
                if (_ch == '=')
                {
                    NextChar();
                    tokenId = TokenId.ExclamationEqual;
                }
                else
                {
                    tokenId = TokenId.Exclamation;
                }
                break;

            case '%':
                NextChar();
                tokenId = TokenId.Percent;
                break;

            case '&':
                NextChar();
                if (_ch == '&')
                {
                    NextChar();
                    tokenId = TokenId.DoubleAmpersand;
                }
                else
                {
                    tokenId = TokenId.Ampersand;
                }
                break;

            case '(':
                NextChar();
                tokenId = TokenId.OpenParen;
                break;

            case ')':
                NextChar();
                tokenId = TokenId.CloseParen;
                break;

            case '{':
                NextChar();
                tokenId = TokenId.OpenCurlyParen;
                break;

            case '}':
                NextChar();
                tokenId = TokenId.CloseCurlyParen;
                break;

            case '*':
                NextChar();
                tokenId = TokenId.Asterisk;
                break;

            case '+':
                NextChar();
                tokenId = TokenId.Plus;
                break;

            case ',':
                NextChar();
                tokenId = TokenId.Comma;
                break;

            case '-':
                NextChar();
                tokenId = TokenId.Minus;
                break;

            case '.':
                NextChar();
                tokenId = TokenId.Dot;
                break;

            case '/':
                NextChar();
                tokenId = TokenId.Slash;
                break;

            case ':':
                NextChar();
                tokenId = TokenId.Colon;
                break;

            case '<':
                NextChar();
                if (_ch == '=')
                {
                    NextChar();
                    tokenId = TokenId.LessThanEqual;
                }
                else if (_ch == '>')
                {
                    NextChar();
                    tokenId = TokenId.LessGreater;
                }
                else if (_ch == '<')
                {
                    NextChar();
                    tokenId = TokenId.DoubleLessThan;
                }
                else
                {
                    tokenId = TokenId.LessThan;
                }
                break;

            case '=':
                NextChar();
                if (_ch == '=')
                {
                    NextChar();
                    tokenId = TokenId.DoubleEqual;
                }
                else if (_ch == '>')
                {
                    NextChar();
                    tokenId = TokenId.Lambda;
                }
                else
                {
                    tokenId = TokenId.Equal;
                }
                break;

            case '>':
                NextChar();
                if (_ch == '=')
                {
                    NextChar();
                    tokenId = TokenId.GreaterThanEqual;
                }
                else if (_ch == '>')
                {
                    NextChar();
                    tokenId = TokenId.DoubleGreaterThan;
                }
                else
                {
                    tokenId = TokenId.GreaterThan;
                }
                break;

            case '?':
                NextChar();
                if (_ch == '?')
                {
                    NextChar();
                    tokenId = TokenId.NullCoalescing;
                }
                else if (_ch == '.')
                {
                    NextChar();
                    tokenId = TokenId.NullPropagation;
                }
                else
                {
                    tokenId = TokenId.Question;
                }
                break;

            case '[':
                NextChar();
                tokenId = TokenId.OpenBracket;
                break;

            case ']':
                NextChar();
                tokenId = TokenId.CloseBracket;
                break;

            case '|':
                NextChar();
                if (_ch == '|')
                {
                    NextChar();
                    tokenId = TokenId.DoubleBar;
                }
                else
                {
                    tokenId = TokenId.Bar;
                }
                break;

            case '"':
            case '\'':
                var quoteIsBalanced = false;
                var quote = _ch;

                NextChar();

                while (_textPos < _textLen && _ch != quote)
                {
                    var nextChar = PeekNextChar();

                    if (_ch == '\\')
                    {
                        if (EscapeCharacters.Contains(nextChar))
                        {
                            NextChar();
                        }

                        if (nextChar == '"')
                        {
                            NextChar();
                        }
                    }

                    NextChar();

                    if (_ch == quote)
                    {
                        quoteIsBalanced = !quoteIsBalanced;
                    }
                }

                if (_textPos == _textLen && !quoteIsBalanced)
                {
                    throw ParseError(_textPos, Res.UnterminatedStringLiteral);
                }

                NextChar();

                tokenId = TokenId.StringLiteral;
                break;

            default:
                if (char.IsLetter(_ch) || _ch is '@' or '_' or '$' or '^' or '~')
                {
                    do
                    {
                        NextChar();
                    } while (char.IsLetterOrDigit(_ch) || _ch == '_');
                    tokenId = TokenId.Identifier;
                    break;
                }

                if (char.IsDigit(_ch))
                {
                    tokenId = TokenId.IntegerLiteral;
                    do
                    {
                        NextChar();
                    } while (char.IsDigit(_ch));

                    bool binaryInteger = false;
                    if (_ch is 'B' or 'b')
                    {
                        NextChar();
                        ValidateBinaryChar();
                        do
                        {
                            NextChar();
                        } while (IsZeroOrOne(_ch));

                        binaryInteger = true;
                    }

                    if (binaryInteger)
                    {
                        break;
                    }

                    var isHexInteger = false;
                    if (_ch is 'X' or 'x')
                    {
                        NextChar();
                        ValidateHexChar();
                        do
                        {
                            NextChar();
                        } while (IsHexChar(_ch));

                        isHexInteger = true;
                    }

                    if (_ch is 'U' or 'L')
                    {
                        NextChar();
                        if (_ch == 'L')
                        {
                            if (_text[_textPos - 1] == 'U')
                            {
                                NextChar();
                            }
                            else
                            {
                                throw ParseError(_textPos, Res.InvalidIntegerQualifier, _text.Substring(_textPos - 1, 2));
                            }
                        }
                        ValidateExpression();
                        break;
                    }

                    if (isHexInteger)
                    {
                        break;
                    }

                    if (_ch == _numberDecimalSeparator)
                    {
                        tokenId = TokenId.RealLiteral;
                        NextChar();
                        ValidateDigit();
                        do
                        {
                            NextChar();
                        } while (char.IsDigit(_ch));
                    }

                    if (_ch is 'E' or 'e')
                    {
                        tokenId = TokenId.RealLiteral;
                        NextChar();
                        if (_ch is '+' or '-')
                        {
                            NextChar();
                        }

                        ValidateDigit();
                        do
                        {
                            NextChar();
                        } while (char.IsDigit(_ch));
                    }

                    if (_ch is 'F' or 'f')
                    {
                        NextChar();
                    }

                    if (_ch is 'D' or 'd')
                    {
                        NextChar();
                    }

                    if (_ch is 'M' or 'm')
                    {
                        NextChar();
                    }

                    break;
                }

                if (_textPos == _textLen)
                {
                    tokenId = TokenId.End;
                    break;
                }

                throw ParseError(_textPos, Res.InvalidCharacter, _ch);
        }

        CurrentToken.Pos = tokenPos;
        CurrentToken.Text = _text.Substring(tokenPos, _textPos - tokenPos);
        CurrentToken.OriginalId = tokenId;
        CurrentToken.Id = GetAliasedTokenId(tokenId, CurrentToken.Text);
    }

    /// <summary>
    /// Check if the current token is the specified <see cref="TokenId"/>.
    /// </summary>
    /// <param name="tokenId">The tokenId to check.</param>
    /// <param name="errorMessage">The (optional) error message.</param>
    public void ValidateToken(TokenId tokenId, string? errorMessage = null)
    {
        if (CurrentToken.Id != tokenId)
        {
            throw ParseError(errorMessage ?? Res.SyntaxError);
        }
    }

    /// <summary>
    /// Check if the current token is an <see cref="TokenId.Identifier"/> with the provided id .
    /// </summary>
    /// <param name="id">The id</param>
    public bool TokenIsIdentifier(string id)
    {
        return CurrentToken.Id == TokenId.Identifier && string.Equals(id, CurrentToken.Text, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Try to get a token based on the id or <see cref="TokenId"/>.
    /// </summary>
    /// <param name="ids">The ids.</param>
    /// <param name="tokenIds">The tokenIds.</param>
    /// <param name="token">The found token, or default when not found.</param>
    public bool TryGetToken(string[] ids, TokenId[] tokenIds, out Token token)
    {
        token = default;

        if (ids.Any(TokenIsIdentifier))
        {
            token = CurrentToken;
            return true;
        }

        if (tokenIds.Any(tokenId => tokenId == CurrentToken.Id))
        {
            token = CurrentToken;
            return true;
        }

        return false;
    }

    private void SetTextPos(int pos)
    {
        _textPos = pos;
        _ch = _textPos < _textLen ? _text[_textPos] : '\0';
    }

    private void NextChar()
    {
        if (_textPos < _textLen)
        {
            _textPos++;
        }
        _ch = _textPos < _textLen ? _text[_textPos] : '\0';
    }

    private void ValidateExpression()
    {
        if (char.IsLetterOrDigit(_ch))
        {
            throw ParseError(_textPos, Res.ExpressionExpected);
        }
    }

    private void ValidateDigit()
    {
        if (!char.IsDigit(_ch))
        {
            throw ParseError(_textPos, Res.DigitExpected);
        }
    }

    private void ValidateHexChar()
    {
        if (!IsHexChar(_ch))
        {
            throw ParseError(_textPos, Res.HexCharExpected);
        }
    }

    private void ValidateBinaryChar()
    {
        if (!IsZeroOrOne(_ch))
        {
            throw ParseError(_textPos, Res.BinaryCharExpected);
        }
    }

    private Exception ParseError(string format, params object[] args)
    {
        return ParseError(CurrentToken.Pos, format, args);
    }

    private TokenId GetAliasedTokenId(TokenId tokenId, string alias)
    {
        return tokenId == TokenId.Identifier && _predefinedOperatorAliases.TryGetValue(alias, out TokenId id) ? id : tokenId;
    }

    private static Exception ParseError(int pos, string format, params object[] args)
    {
        return new ParseException(string.Format(CultureInfo.CurrentCulture, format, args), pos);
    }

    private static bool IsHexChar(char c)
    {
        if (char.IsDigit(c))
        {
            return true;
        }

        if (c <= '\x007f')
        {
            c |= (char)0x20;
            return c is >= 'a' and <= 'f';
        }

        return false;
    }

    private static bool IsZeroOrOne(char c)
    {
        return c is '0' or '1';
    }
}