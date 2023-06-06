using System.Text.RegularExpressions;

namespace Solution
{
    internal class Solution
    {
        // -----------------------------------------------------------------
        // Uff, this assignment was the crazy one! Thanks, it was really fun
        // -----------------------------------------------------------------

        // -------
        // Parsing
        // -------
        internal sealed class DataParser
        {
            private static readonly Regex ValuesAndBracketsPattern = new(@"\([A-Z]{1}\,[A-Z]{1}\)", RegexOptions.Compiled);

            internal ErrorsReporter ErrorsReporter { get; }

            internal DataParser(ErrorsReporter errorsReporter)
            {
                ErrorsReporter = errorsReporter;
            }

            internal (char ParentValue, char ChildValue)[] ExtractData(string rawInput)  // Total: O(n)
            {
                return ExtractPairs(rawInput).Select(nodesPair =>  // O(n) + O(n) => 2 O(n) => O(n)
                {
                    char[] splittedLetters = ExtractValues(nodesPair);  // O(1)

                    char parentValue = splittedLetters[0];  // O(1)
                    char childValue = splittedLetters[1];   // O(1)

                    return (parentValue, childValue);
                })
                .ToArray();
            }

            private static string[] ExtractPairs(string rawInput)  // Total: O(n)
            {
                return rawInput.Split(' ');
            }

            /// <summary>
            /// Produces <see cref="Errors.InvalidInput"/> error.
            /// </summary>
            private char[] ExtractValues(string bracketsAndLetters)  // Total: O(1)
            {
                if (string.IsNullOrWhiteSpace(bracketsAndLetters) ||        // O(1)
                    !ValuesAndBracketsPattern.IsMatch(bracketsAndLetters))  // O(1)
                {
                    ErrorsReporter.Report(Errors.InvalidInput);  // O(1)
                }

                // NOTE: The following structure should always be the same:
                // 0 - '('
                // 1 - first letter
                // 2 - ','
                // 3 - second letter
                // 4 - ')'
                char firstLetter = bracketsAndLetters[1];
                char secondLetter = bracketsAndLetters[3];

                return new[] { firstLetter, secondLetter };
            }
        }

        // ------
        // Errors
        // ------
        internal enum Errors
        {
            InvalidInput = 1,
            DuplicatePair = 2,
            TooManyChildren = 3,
            CycleDetected = 4,
            MultipleRoots = 5,

            NoErrors = 999
        }

        internal sealed class ErrorsReporter
        {
            private readonly Dictionary<Errors, string> _errorCodes = new()
            {
                { Errors.InvalidInput,    "E1" },
                { Errors.DuplicatePair,   "E2" },
                { Errors.TooManyChildren, "E3" },
                { Errors.CycleDetected,   "E4" },
                { Errors.MultipleRoots,   "E5" }
            };

            private Errors _currentErrorLevel = Errors.NoErrors;

            internal void Report(Errors errorLevel)  // Total: O(1)
            {
                // There is no more severe error than this one + everything relies on the data, do not hesitate with reporting
                if (errorLevel == Errors.InvalidInput)
                {
                    throw new InvalidOperationException(_errorCodes[errorLevel]);
                }

                // Prioritize errors
                if (errorLevel < _currentErrorLevel)  // O(1)
                {
                    _currentErrorLevel = errorLevel;
                }
            }

            internal bool ErrorsOccurred(out string highestErrorCode)  // Total: O(1)
            {
                highestErrorCode = string.Empty;

                if (_currentErrorLevel != Errors.NoErrors)
                {
                    highestErrorCode = _errorCodes[_currentErrorLevel];

                    return true;
                }

                return false;
            }
        }

        // -----
        // Graph
        // -----
        internal enum GraphSide
        {
            Left = 0,
            Right = 1
        }

        internal sealed class Graph
        {
            private readonly DataParser _dataParser;
            private readonly ErrorsReporter _errorsReporter;

            private Dictionary<char, Node>? _nodesRegister;

            internal Graph(DataParser dataParser)
            {
                _dataParser = dataParser;
                _errorsReporter = dataParser.ErrorsReporter;
            }

            internal string Build(string input)  // Best: O(n), Worst: O(n^2) => The logic to determine "root node(s)" can be improved
            {
                try
                {
                    // Pre-validation
                    GuardAgainstInvalidInput(input);  // O(n)

                    // Data preparation
                    (char ParentValue, char ChildValue)[] extractedNodesData = _dataParser.ExtractData(input);  // O(n)

                    // Late initialization of register to gain O(1) complexities for "Add()" operations later
                    _nodesRegister = new(extractedNodesData.Length);

                    // Graph creation
                    foreach ((char ParentValue, char ChildValue) nodes in extractedNodesData)  // O(n) or O(n^2) depends on "ConnectNodes"
                    {
                        Node parent = TryRegisterParent(nodes.ParentValue, nodes.ChildValue);  // O(1)
                        ConnectNodes(parent, nodes.ChildValue);  // O(1) or O(n) where "n" is length of a graph's branch
                    }

                    // Graph traversing
                    string binaryTreeSequence = string.Empty;

                    if (IsRootDetermined(out Node? rootNode))  // O(n) => we can try to cache root node somewhere between operations
                    {
                        binaryTreeSequence = ProduceSequenceOutput(rootNode!);  // O(n)
                    }

                    // Post-validation
                    return _errorsReporter.ErrorsOccurred(out string highestErrorCode)  // O(1)
                        ? highestErrorCode
                        : binaryTreeSequence;
                }
                catch (Exception exception)  // Just in case
                {
                    return exception.Message;
                }
            }

            /// <summary>
            /// Produces <see cref="Errors.InvalidInput"/> error.
            /// </summary>
            private void GuardAgainstInvalidInput(string input)  // Total: O(n)
            {
                if (input?.Length == 0 || input?[0] == ' ' || input?[^1] == ' ')  // O(n)
                {
                    _errorsReporter.Report(Errors.InvalidInput);  // O(1)
                }
            }

            /// <summary>
            /// Produces <see cref="Errors.CycleDetected"/> error.
            /// </summary>
            private bool GuardAgainstReassigningParent(char childValue)  // Total: O(1)
            {
                if (_nodesRegister!.TryGetValue(childValue, out Node? existingNode))
                {
                    if (existingNode.Parent != null)  // Attaching as parent to an element which already have a parent
                    {
                        _errorsReporter.Report(Errors.CycleDetected);  // O(1)

                        return true;
                    }
                }

                return false;
            }

            /// <summary>
            /// Produces <see cref="Errors.CycleDetected"/> error.
            /// </summary>
            private bool GuardAgainstDeathLoop(Node node, char childValue)  // Total: O(n) in the worst case
            {
                Node? parent = node.Parent;

                while (parent != null)  // O(n) => potentially the worst case for END to START traversal on a single-branched graph
                {
                    if (childValue == parent.Value)
                    {
                        _errorsReporter.Report(Errors.CycleDetected);  // O(1)

                        return true;  // Attempt to assigning your own parent as your child
                    }

                    parent = parent.Parent;
                }

                return false;
            }

            /// <summary>
            /// Produces <see cref="Errors.DuplicatePair"/>, <see cref="Errors.TooManyChildren"/> errors.
            /// </summary>
            private Node TryRegisterParent(char parentValue, char childValue)  // Total: O(1)
            {
                // Validate existing parent
                if (_nodesRegister!.TryGetValue(parentValue, out Node? existingParent))  // O(1)
                {
                    // Pair with either left (1st) or right (2nd) child are already existing: O(1)
                    if (existingParent.LeftChild?.Value == childValue ||
                        existingParent.RightChild?.Value == childValue)
                    {
                        _errorsReporter.Report(Errors.DuplicatePair);  // O(1)
                    }
                    // Both children are already present but a 3rd unique one is also trying to be assigned: O(1)
                    else if (existingParent.LeftChild != null &&
                             existingParent.RightChild != null)
                    {
                        _errorsReporter.Report(Errors.TooManyChildren);  // O(1)
                    }

                    // NOTE: Let's continue. An existing parent has enough room to invite one more child
                    return existingParent;
                }
                // Create new parent
                else
                {
                    Node newParent = new(parentValue);

                    _nodesRegister.Add(parentValue, newParent);  // O(1)

                    return newParent;
                }
            }

            private void TryRegisterChild(Node child)  // Total: O(1)
            {
                if (!_nodesRegister!.ContainsKey(child.Value))  // O(1)
                {
                    _nodesRegister.Add(child.Value, child);  // O(1)
                }
            }

            /// <summary>
            /// Produces <see cref="Errors.CycleDetected"/> error.
            /// </summary>
            private void ConnectNodes(Node parent, char childValue)  // Total: O(n)
            {
                // Prevent overriding an already assigned parent
                if (GuardAgainstReassigningParent(childValue))  // O(1)
                {
                    return;
                }

                Node child = _nodesRegister!.TryGetValue(childValue, out Node? existingChild)  // O(1)
                    ? existingChild  // Use an already existing node
                    : new(childValue);  // Create new child

                Node? previousParent = child.Parent;
                child.Parent = parent;  // Attach to parent to connect abandoned branches of the tree

                // Prevent assigning parent of this node as its child
                if (GuardAgainstDeathLoop(parent, childValue))  // Best: O(1) => no conflicts, Worst: O(n)
                {
                    child.Parent = previousParent;  // Revert changes
                }
                else
                {
                    // Assigning child to its new parent: O(1)
                    if (parent.LeftChild == null)
                    {
                        parent.LeftChild = child;
                    }
                    else
                    {
                        parent.RightChild ??= child;
                    }

                    // Swapping children (to keep them lexicographically ordered): O(1)
                    if (parent.RightChild?.Value != null &&
                        parent.LeftChild.Value > parent.RightChild.Value)
                    {
                        // Cool trick. No need to initialize temp variables anymore. Wohoo!
                        (parent.LeftChild, parent.RightChild) = (parent.RightChild, parent.LeftChild);
                    }

                    // Register
                    TryRegisterChild(child);  // O(1)
                }
            }

            /// <summary>
            /// Produces <see cref="Errors.MultipleRoots"/> error.
            /// </summary>
            private bool IsRootDetermined(out Node? rootNode)  // Total: O(n)
            {
                rootNode = default;
                int rootsCount = 0;

                foreach (KeyValuePair<char, Node> pairs in _nodesRegister!)  // O(n)
                {
                    Node currentNode = pairs.Value;

                    if (currentNode.IsRoot)  // O(1)
                    {
                        rootNode = currentNode;
                        rootsCount++;
                    }
                }

                if (rootsCount > 1)
                {
                    _errorsReporter.Report(Errors.MultipleRoots);  // O(1)

                    return false;
                }

                return true;
            }

            // Recursively traverse the given graph
            private static string ProduceSequenceOutput(Node node)  // Total: O(n)
            {
                return Wrap(node);  // O(n) => all nodes will be visited. No fancy Big-O here
            }

            private static string Wrap(Node? node)  // Total: O(n)
            {
                if (node?.LeftChild == null && node?.RightChild == null)
                {
                    //           Empty child / no node   End node without children
                    return node == null ? string.Empty : $"({node?.Value})";
                }

                // Nested pairs ("injected" into another nested pairs)
                return $"({node?.Value}{Wrap(node?.LeftChild)}{Wrap(node?.RightChild)})";
            }
        }

        internal sealed class Node
        {
            internal char Value { get; }

            internal Node? Parent { get; set; }

            internal Node? LeftChild { get; set; }

            internal Node? RightChild { get; set; }

            internal bool IsRoot => Parent == null;

            internal Node(char value)
            {
                Value = value;
            }
        }

        // --------
        // Workflow
        // --------
        private static void Main()
        {
            string rawInput = Console.ReadLine()!;

            ErrorsReporter errorsReporter = new();
            DataParser dataParser = new(errorsReporter);
            Graph graph = new(dataParser);

            string result = graph.Build(rawInput);

            Console.WriteLine(result);
        }
    }
}
