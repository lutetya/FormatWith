﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FormatWith {
    public static class FormatHelpers {
        public static string ProcessTokens(IEnumerable<FormatToken> tokens, IDictionary<string, object> replacements, MissingKeyBehaviour missingKeyBehaviour = MissingKeyBehaviour.ThrowException, string fallbackReplacementValue = null) {

            // if there are no parameters, return the format string unmodified.
            if (!tokens.Any()) {
                return string.Empty;
            }

            // create a stringbuilder to hold the resultant output string
            // use the input format string length as a ballpark starting figure for the buffer size
            StringBuilder resultBuilder = new StringBuilder(tokens.First().Length);

            foreach (FormatToken thisToken in tokens) {

                // handle text token
                TextToken textToken = thisToken as TextToken;
                if (textToken != null) {
                    // token is a text token
                    // add the token to the result string builder
                    resultBuilder.Append(textToken.SourceString, textToken.StartIndex, textToken.Length);
                }
                else {
                    ParameterToken parameterToken = thisToken as ParameterToken;
                    if (parameterToken != null) {
                        // token is a parameter token
                        // perform parameter logic now.

                        // append the replacement for this parameter
                        object replacementValue = null;
                        if (replacements.TryGetValue(parameterToken.ParameterKey, out replacementValue)) {
                            // the key exists, add the replacement value
                            // this does nothing if replacement value is null
                            resultBuilder.Append(replacementValue);
                        }
                        else {
                            // the key does not exist, handle this using the missing key behaviour specified.
                            switch (missingKeyBehaviour) {
                                case MissingKeyBehaviour.ThrowException:
                                    // the key was not found as a possible replacement, throw exception
                                    throw new FormatException($"The parameter \"{parameterToken.ParameterKey}\" was not present in the lookup dictionary");
                                case MissingKeyBehaviour.ReplaceWithFallback:
                                    resultBuilder.Append(fallbackReplacementValue);
                                    break;
                                case MissingKeyBehaviour.Ignore:
                                    // the replacement value is the input key as a parameter.
                                    // use source string and start/length directly with append rather than
                                    // parameter.ParameterKey to avoid allocating an extra string
                                    resultBuilder.Append(parameterToken.SourceString, parameterToken.StartIndex, parameterToken.Length);
                                    break;
                            }
                        }
                    }
                    else {
                        // parameter was null, throw null reference exception
                        throw new NullReferenceException($"A format token in {nameof(tokens)} was null");
                    }
                }
            }

            // return the resultant string
            return resultBuilder.ToString();
        }

        public static List<string> GetParameters(string formatString, char openBraceChar = '{', char closeBraceChar = '}') {
            return Tokenize(formatString, openBraceChar, closeBraceChar)
                .Select(t => t as ParameterToken)
                .Where(pt => pt != null)
                .Select(pt => pt.ParameterKey)
                .ToList();
        }

        public static IReadOnlyCollection<FormatToken> Tokenize(string formatString, char openBraceChar = '{', char closeBraceChar = '}') {

            if (formatString == null) throw new ArgumentNullException($"{nameof(formatString)} cannot be null.");

            List<FormatToken> parameters = new List<FormatToken>();

            int currentTokenStart = 0;

            // start the state machine!

            bool insideBraces = false;

            int index = 0;
            while (index < formatString.Length) {
                if (!insideBraces) {
                    // currently not inside a pair of braces in the format string
                    if (formatString[index] == openBraceChar) {
                        // check if the brace is escaped
                        if (index < formatString.Length - 1 && formatString[index + 1] == openBraceChar) {
                            // ESCAPED OPEN BRACE
                            // we have hit an escaped open brace
                            // add the current normal text, as well as the first brace, to the
                            // list of tokens as a text token.
                            parameters.Add(new TextToken(formatString, currentTokenStart, (index - currentTokenStart) + 1));

                            // skip over braces
                            index += 2;

                            // set new current token start and current token length
                            currentTokenStart = index;

                            continue;
                        }
                        else {
                            // not an escaped brace, set state to inside brace
                            insideBraces = true;

                            // START OF PARAMETER

                            // we are leaving standard text and entering into a parameter
                            // add the text traveresed so far as a text token
                            if (currentTokenStart < index) {
                                parameters.Add(new TextToken(formatString, currentTokenStart, (index - currentTokenStart)));
                            }

                            // set the start index of the token to the start of this parameter
                            currentTokenStart = index;

                            index++;

                            continue;
                        }
                    }
                    else if (formatString[index] == closeBraceChar) {
                        // handle case where closing brace is encountered outside braces
                        if (index < formatString.Length - 1 && formatString[index + 1] == closeBraceChar) {
                            // this is an escaped closing brace, this is okay

                            // add the current normal text, as well as the first brace, to the
                            // list of tokens as a text token.
                            parameters.Add(new TextToken(formatString, currentTokenStart, (index - currentTokenStart) + 1));

                            // skip over braces
                            index += 2;

                            // set new current token start and current token length
                            currentTokenStart = index;

                            continue;
                        }
                        else {
                            // this is an unescaped closing brace outside of braces.
                            // throw a format exception
                            throw new FormatException($"Unexpected closing brace at position {index}");
                        }
                    }
                    else {
                        // move onto next character
                        index++;
                        continue;
                    }
                }
                else {
                    // currently inside a pair of braces in the format string                   
                    if (formatString[index] == openBraceChar) {
                        // found an opening brace
                        // check if the brace is escaped
                        if (index < formatString.Length - 1 && formatString[index + 1] == openBraceChar) {
                            // there are escaped braces within the key
                            // this is illegal, throw a format exception
                            throw new FormatException($"Illegal escaped opening braces within a parameter at position {index}");
                        }
                        else {
                            // not an escaped brace, we have an unexpected opening brace within a pair of braces
                            throw new FormatException($"Unexpected opening brace inside a parameter at position {index}");
                        }
                    }
                    else if (formatString[index] == closeBraceChar) {
                        // END OF PARAMETER
                        // handle case where closing brace is encountered inside braces
                        // don't attempt to check for escaped braces here - always assume the first brace closes the braces
                        // since we cannot have escaped braces within parameters.

                        // Add the parameter information to the parameter list
                        parameters.Add(new ParameterToken(formatString, currentTokenStart, (index - currentTokenStart) + 1));

                        // set the state to be outside of any braces
                        insideBraces = false;

                        // jump over brace
                        index++;

                        // update current token start
                        currentTokenStart = index;

                        // jump to next state
                        continue;
                    } // if }
                    else {
                        // character has no special meaning, it is part of the current key
                        // move onto next character
                        index++;
                        continue;
                    } // else
                } // if inside brace
            } // while index < formatString.Length

            // after the loop, if all braces were balanced, we should be outside all braces
            // if we're not, the input string was misformatted.
            if (insideBraces) {
                throw new FormatException($"The format string ended before the parameter was closed. Position {index}");
            }
            else {
                // outside braces. Add on any remaining text at the end of the format string
                if (currentTokenStart < index) {
                    parameters.Add(new TextToken(formatString, currentTokenStart, (index - currentTokenStart)));
                }
            }

            return parameters.AsReadOnly();
        }
    }
}