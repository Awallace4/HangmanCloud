// WordGridScript.js
// This file contains the client-side script for the WordGrid word game.
// This includes the code to:
// 1. Handle client-side events including drag and drop of game tiles.
//    See allowDrop, dropItem, and dragItem for details.
// 2. Handle UI actions such as the Submit Play, Swap Tiles, and Recall Tiles buttons.
//    see capturePlayDetails, swapTiles, and recallTiles.
// 3. Handle checking the user's play for validity and computing its score.
//    see scoreForPlay, verifyPlay.
// 4. Handle submitting the move information to the server.
//    See capturePlayDetails.
// 5. Maintain some internal state in global variables, only during the duration
//    of a single user play.

// This code does not use any non-default Javascript libraries.

// Global constants
var BoardSize = 13;
var CenterSquare = 6 * BoardSize + 6;
var MaxTilesInRack = 6;

var Direction = { "Across": 0, "Down": 1, "Single": 2 }
var SpaceType = { "Normal": 0, "DLS": 1, "DWS": 2, "TLS": 3, "TWS": 4 }

var tilesOnBoard;
var playedTiles;
var playedCharacters = new Array();

var oldCellIds = new Array();
var oldRows = new Array();
var oldCols = new Array();

var newCellIds = new Array();
var newRows = new Array();
var newCols = new Array();

var gameBoard = new Array(BoardSize * BoardSize);
var newBoard = new Array(BoardSize * BoardSize);

document.onkeyup = OnKey
document.onload = OnLoad

var currentRow = Math.round(BoardSize / 2 - 0.5)
var currentCol = Math.round(BoardSize / 2 - 0.5)
var currentCell = CenterSquare

function convertToChar(keyID) {
    //var chars = ["A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M", "N", "O", "P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z" ]
    var chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    return chars.charAt(keyID - 65)
}

function OnLoad() {
    setCurrentCellBorder()
}

function OnClick(e) {
    if (!e) {
        e = window.event
    }
    var elementID = e.srcElement.getAttribute('id')
    // The cell has the id "imgspace" + a number, so get number starting at index 8.
    var cellID = elementID.substr(8)
    resetCellBorder()
    currentCell = cellID
    currentRow = Math.round  (currentCell / BoardSize - 0.5);
    currentCol = currentCell % BoardSize;
    setCurrentCellBorder()
    updateBoard()
    var userMessageElement = document.getElementById("userMessage")
    userMessageElement.textContent = " CurrentCell: " + currentCell + " Row, Col: (" + currentRow + ", " + currentCol + ")"
}

function resetCellBorder() {
    var oldCurrentCell = currentCell
    var oldCurrentElement = document.getElementById("imgspace" + oldCurrentCell)
    oldCurrentElement.style.border = "1px solid white"
}
function setCurrentCellBorder() {
    var currentElement = document.getElementById("imgspace" + currentCell)
    currentElement.style.border = "1px dotted red"
}

function OnKey(e) {
    var keyID = (window.event) ? event.keyCode : e.keyCode;

    // debug code follows
    var userMessageElement = document.getElementById("userMessage")
    if (keyID >= 65 && keyID <= 90) {

        // Letter 
        userMessageElement.textContent = "Letter Code: " + keyID.toString()
        // If the user has a tile that matches this letter in the rack,
        // then place the tile at the current position.
        // If not, then beep.
        // The tile could be matched by a blank or an actual tile.
        // Advance the position by 1 depending on the current direction.
        var keyChar = convertToChar(keyID)
        var tileRack = document.getElementById("tileRack")
        if (tileRack.hasChildNodes) {
            var nodes = tileRack.getElementsByClassName("rackTile")
            var found = false
            var count = 0
            for (count = 0; count < nodes.length && !found; count++) {
                var altAttribute = nodes[count].attributes["alt"]
                var letterChar = altAttribute.value.charAt(0)
                if (letterChar == keyChar) {
                    found = true
                    break;
                }
            }
            if (found) {
                var boardSpace = document.getElementById("imgspace" + currentCell)
                var newParent = boardSpace.parentNode
                var movedItem = nodes[count]
                var oldParent = movedItem.parentNode;    
                oldParent.removeChild(movedItem);
                movedItem.className = "playedTile";
                boardSpace.style.display = "none";
                newParent.appendChild(movedItem);
                updateBoard();
            }
            else {
                //alert("Error")
            }
        }
    } else {
        switch (keyID) {
            case 37:
                // Left Arrow
                userMessageElement.textContent = "Left Arrow"
                currentCol = (currentCol - 1 + BoardSize) % BoardSize;
                break;
            case 38:
                // Up Arrow
                userMessageElement.textContent = "Up Arrow"
                currentRow = (currentRow - 1 + BoardSize) % BoardSize;
                break;
            case 39:
                // Right Arrow
                userMessageElement.textContent = "Right Arrow"
                currentCol = (currentCol + 1) % BoardSize;
                break;
            case 40:
                // Down Arrow
                userMessageElement.textContent = "Down Arrow"
                currentRow = (currentRow + 1) % BoardSize;
                break;

            case 32:
                // TODO: put a blank tile on the board
                userMessageElement.textContent = "Space Bar"
                break;
            default:
                userMessageElement.textContent = "Code " + keyID.toString()
                break;
        }

    }
    resetCellBorder()
    currentCell = currentRow * BoardSize + currentCol;
    setCurrentCellBorder()
    userMessageElement.textContent += " (" + currentRow + ", " + currentCol + ")";
}

    // Gets an existing board tile at a given row or column,
    // or returns null if the space is vacant or has 
    // a new tile on it.
    function getOldTile(row, col) {
        // Try to find the tile and get its alt attribute 
        // which has the tile letter and point value
        var cellId = row * BoardSize + col;
        var tileElement = document.getElementById("imgspace" + cellId);
        var attrText = tileElement.getAttribute("alt");
        var tileLetter = attrText.substr(0, 1);
        var tilePoints = parseInt(attrText.substr(1));
        tile = { letter: tileLetter, pointValue: tilePoints };
        return tile;
    }

    // Gets a played tile at the given row and column, or null
    // if the space is vacant or contains an old tile.
    function getNewTile(row, col) {
        // Try to find the tile and get its alt attribute 
        // which has the tile letter and point value
        var cellId = row * BoardSize + col;
        for (count = 0; count < playedTiles.length; count++) {
            var playedTile = playedTiles[count];
            // determine the row and col of this tile
            var parentId = playedTile.parentElement.getAttribute("id");
            var parentCellId = parseInt(parentId.substr(5));
            if (parentCellId == cellId) {
                var attrText = playedTile.getAttribute("alt");
                // The first character of the "alt" attribute is the tile letter
                var tileLetter = attrText.substr(0, 1);
                // The point value follows the tile letter in the "alt" attribute
                var tilePoints = parseInt(attrText.substr(1));
                tile = { letter: tileLetter, pointValue: tilePoints };
                return tile;
            }
        }
        return null;
    }

    // Get a tile at a certain square, old or new.
    // Return null if the space is empty.
    function getTile(row, col) {
        var tile = getNewTile();
        if (tile == null) {
            tile = getOldTile();
        }
        return tile;
    }

    // Builds the game board in memory, not including new tiles.
    function buildGameBoard() {
        // Build an array of BoardSize * BoardSize with objects that can be
        // either null or a tile with a given letter value and point value.
        var board = new Array(BoardSize * BoardSize);
        for (row = 0; row < BoardSize; row++) {
            for (col = 0; col < BoardSize; col++) {
                var tile = null;
                if (oldCellIds.indexOf(row * BoardSize + col) != -1)
                {
                    tile = getOldTile(row, col);
                }
                board[row*BoardSize + col] = tile;
            }
        }
        return board;
    }

    // Builds an array of the game board including the new tiles the user
    // is currently playing on the board.
    function buildNewBoard() {
        var board = new Array(BoardSize * BoardSize);
        for (row = 0; row < BoardSize; row++) {
            for (col = 0; col < BoardSize; col++) {
                var tile = null;
                if (oldCellIds.indexOf(row * BoardSize + col) != -1) {
                    tile = getOldTile(row, col);
                    //assert(tile != null);
                } else if (newCellIds.indexOf(row * BoardSize + col) != -1) {
                    tile = getNewTile(row, col);
                    //assert(tile != null);
                }
                board[row * BoardSize + col] = tile;
            }
        }
        return board;
    }

    // Determines if a tile is adjacent to a played tile
    // Used to determine if a play is valid
    // True if any tile up, down, left or right of a square
    // is a played tile.
    function isAdjacent(row, col) {
        var returnValue = false;
        if (row < BoardSize) {
            returnValue = returnValue || isBoardTile(row + 1, col);
        }
        if (row > 0) {
            returnValue = returnValue || isBoardTile(row - 1, col);
        }
        if (col < BoardSize) {
            returnValue = returnValue || isBoardTile(row, col + 1);
        }
        if (col > 0) {
            returnValue = returnValue || isBoardTile(row, col - 1);
        }
        return returnValue;
    }

    // Returns true if the tile at row, col is a newly played tile, as opposed to one
    // that was already on the board at the start of this play.
    function isPlayedTile(row, col) {
        return newCellIds.indexOf(row * BoardSize + col) != -1;
    }

    // Returns true if a tile at row, col is not a newly played tile.
    function isBoardTile(row, col) {
        return oldCellIds.indexOf(row * BoardSize + col) != -1;
    }

    // Returns true if the given row, col has no tile.
    function isEmptySpace(row, col) {
        return ! (isPlayedTile(row, col) || isBoardTile(row, col));
    }

    // Swap the tiles at one space with another space. (used by Transpose).
    function Swap2D(board, row1, col1, row2, col2)
    {
        var temp = board[row1*BoardSize + col1];
        board[row1*BoardSize + col1] = board[row2*BoardSize + col2];
        board[row2*BoardSize + col2] = temp;
    }

    // Change the board, reversing across and down.
    // Used to simplify scoring and searching for plays.
    function Transpose(board)
    {
        for (row = 0; row < BoardSize; row++)
        {
            for (col = row + 1 ; col < BoardSize; col++)
            {
                Swap2D(board, row, col, col, row);
            }
        }
    }

    // Gets the type of board space at a given location.
    function GetSpace(row, col)
    {
        var cellID = row * BoardSize + col;
        var element = document.getElementById("imgspace" + cellID);
        if (element != null) {
            return parseInt(element.getAttribute('alt'));
        }
        return null;
    }

    // Compute the score for a play.
    function scorePlay() {
        // Score a play.
        // This function assumes the play was already validated.
        var cumScore = 0;
        var wordScore = 0;
        var wordMultiplier = 1;
        var tilesPlayed = 0;
        var isLegalFirstMove = false;
        var colElement = document.getElementById('col');
        var rowElement = document.getElementById('row');
        var row = parseInt(rowElement.value);
        var col = parseInt(colElement.value);
        var mainWord = "";
        var wordsPlayed = new Array();
        var directionElement = document.getElementById('direction');
        var direction = parseInt(directionElement.value);
        var isTransposed = false;

        // When only one tile is played, treat as an across play for
        // purposes of scoring.
        if (direction == Direction.Single) {
            // If only one tile is played, the value of direction is "Direction.Single"
            // We then have to decide whether to treat this as a down or across play
        
            var tilesInRow = 0;
            if (col > 0 && newBoard[row * BoardSize + col - 1] != null) {
                tilesInRow++;
            }
            if (col < BoardSize && newBoard[row * BoardSize + col + 1] != null) {
                tilesInRow++;
            }

            var tilesInCol = 0;
            if (row > 0 && newBoard[(row - 1) * BoardSize + col] != null) {
                tilesInCol++;
            }
            if (row < BoardSize && newBoard[(row + 1) * BoardSize + col] != null) {
                tilesInCol++;
            }
        
            // If there are more adjacent tiles in the same column as in the same row,
            // then treat it as a "down" move.
            if (tilesInRow < tilesInCol)
            {
                direction = Direction.Down;
            }
            else {
                direction = Direction.Across;
            }
        }

        // Transpose the board so that all our calculations assume a
        // play across the board, not down
        if (direction == Direction.Down)
        {
            Transpose(gameBoard);
            Transpose(newBoard);
            var temp = row;
            row = col;
            col = temp;
            isTransposed = true;
        }
        //direction <- Direction.Across;
        //let crossDirection = Direction.Down

        // Find the start of the main word being played
        while (col >= 0 && newBoard[row*BoardSize + col] != null)
        {
            col--;
        }
        col++;

        while (row < BoardSize && col < BoardSize && (newBoard[row*BoardSize + col] != null))
        {
            var letterMultiplier = 1;
            var crossWordMultiplier = 1;
            mainWord.concat(newBoard[row*BoardSize + col].letter)

            // If the board square on the old board was empty, this is a played tile
            // otherwise it's a tile that was already on the board.
            if (gameBoard[row * BoardSize + col] == null) {
                var space = SpaceType.Normal;
                tilesPlayed++;
                if (isTransposed) {
                    space = GetSpace(col, row);
                }
                else {
                    space = GetSpace(row, col);
                }
                switch (space) {
                    case SpaceType.DLS:
                        letterMultiplier = 2;
                        break;
                    case SpaceType.DWS:
                        crossWordMultiplier = 2;
                        wordMultiplier = 2;
                        break;
                    case SpaceType.TLS:
                        letterMultiplier = 3;
                        break;
                    case SpaceType.TWS:
                        crossWordMultiplier = 3;
                        wordMultiplier = 3;
                        break;
                    default: break;
                }

                // identify crosswords by starting at the first played letter
                // and counting adjacent tiles in both directions

                var rowCrossBegin = row;
                var rowCross = row;
                var crossScore = 0;

                // Find the beginning of the crossword (if any) that crosses the current square

                while (rowCrossBegin >= 0 && newBoard[rowCrossBegin * BoardSize + col] != null) {
                    rowCrossBegin--;
                }
                rowCrossBegin++;

                // Now scan forward along the cross word
                var rowCrossEnd = row;

                while (rowCrossEnd < BoardSize && newBoard[rowCrossEnd * BoardSize + col] != null) {
                    rowCrossEnd++;
                }
                rowCrossEnd--;


                if (rowCrossBegin != rowCrossEnd) {
                    // A crossword was found
                    // Parse out the word and score it
                    var crossWord = "";
                    for (var rowCross = rowCrossBegin; rowCross <= rowCrossEnd; rowCross++) {
                        crossWord.concat(newBoard[rowCross * BoardSize + col].letter);
                        if (rowCross == row) {
                            crossScore += newBoard[rowCross * BoardSize + col].pointValue * letterMultiplier;
                        }
                        else {
                            crossScore += newBoard[rowCross * BoardSize + col].pointValue;
                        }
                    }
                    wordsPlayed.concat(crossWord);
                    crossScore *= crossWordMultiplier;
                    cumScore += crossScore;
                }
            }
            wordScore += letterMultiplier * newBoard[row*BoardSize + col].pointValue;
            col++;

        }
        wordsPlayed.concat(mainWord);
        wordScore *= wordMultiplier;
        cumScore += wordScore;
    
        // Bonus for playing all tiles
        if (tilesPlayed == MaxTilesInRack)
        {
            cumScore += 50;
        }

        // If the direction was transposed, restore the original
        if (isTransposed)
        {
            Transpose(gameBoard);
            Transpose(newBoard);
        }

        return cumScore;
    }

    // Update the in-memory representation of the board.
    // Called when the board is changed (a new tile is added, for example).
    function updateBoard() {
        // construct an array of letters that already exist on the board
        // TODO: only do this when the board is initially loaded.
        tilesOnBoard = document.getElementsByClassName("tileOnBoard");

        oldCellIds = new Array();
        oldCols = new Array();
        oldRows = new Array();
        for (var index = 0; index < tilesOnBoard.length ; index++) {
            var tile = tilesOnBoard[index];
            // store the row and col of all the old tiles 
            var idString = tile.parentElement.id;
            idString = idString.substring(5);
            var cellId = parseInt(idString);
            oldCellIds = oldCellIds.concat(cellId);
            oldRows = oldRows.concat(Math.round(cellId / BoardSize - 0.5));
            oldCols = oldCols.concat(cellId % BoardSize);
        }

        // TODO: do this only when the board is initially loaded
        gameBoard = buildGameBoard();

        // construct an array of played tiles
        playedTiles = document.getElementsByClassName("playedTile");
        if (playedTiles.length = 0) {
            scoreElement.innerText = "";
            submitButton.disabled = true;
            return;
        }
        var count = 0;
        var firstCellId = BoardSize * BoardSize + 1;
        newCellIds = new Array();
        newRows = new Array();
        newCols = new Array();
        for (var index = 0; index < playedTiles.length; index++) {
            var tile = playedTiles[index];
            var attr = tile.parentNode.attributes['id'];
            playedCharacters[count++] = tile.getAttribute('alt').substr(0, 1);

            // store the row and col of the new tiles
            var idString = tile.parentNode.id;
            idString = idString.substring(5);
            var cellId = parseInt(idString);
            newCellIds = newCellIds.concat(cellId);
            newRows = newRows.concat(Math.round(cellId / BoardSize - 0.5));
            newCols = newCols.concat(cellId % BoardSize);

            // keep track of the minimum cellId, which is the first tile played
            if (cellId < firstCellId)
                firstCellId = cellId;
        }

        // row and col of the first played tile are easily computed
        // from the cellId of the first played tile
        var firstCol = firstCellId % BoardSize;
        var firstRow = Math.round(firstCellId / BoardSize - 0.5);

        var colElement = document.getElementById('col');
        var rowElement = document.getElementById('row');

        colElement.value = firstCol;
        rowElement.value = firstRow;

        // Update the new board
        newBoard = buildNewBoard();

        var scoreElement = document.getElementById("scoreForPlay");
        var submitButton = document.getElementById("submitPlay");

        // Check to see if the current configuration forms a valid play.
        // If the score element is null, then it's not the user's turn.
        if (scoreElement != null) {
            if (isValidPlay()) {
                var scoreForPlay = scorePlay();
                scoreElement.innerText = "+ " + scoreForPlay.toString();
                // Enable submit button
                submitButton.disabled = false;
            }
            else {
                scoreElement.innerText = "";
                submitButton.disabled = true;
            }
        }

    }

    // Returns true if this is the first move of a game, which means that
    // the user must include the center square in the play.
    function isFirstMove() {
        // It is the first move if there are no board tiles.
        return oldCellIds.length == 0;
    }

    // Determines whether a play on the board is a legal move
    // A play is legal if at least one tile is adjacent
    // all tiles are in a single row or column
    // and the played and unplayed tiles are contiguous (have no gaps).
    function isValidPlay() {

        var direction = -1;
        var isValidDirection = true;
        if (playedTiles.length == 1) {
            // User played only one tile
            // Tiles could be left, right, above and below this single tile
            direction = 2;
        }
        else if (newRows[0] == newRows[1]) {
            var count = 0;
            // Now check any other rows.
            for (row in newRows) {
                if (row[count++] != newRows[0]) {
                    isValidDirection = false;
                }
            }
            direction = 0; // across
        }
        else if (newCols[0] == newCols[1]) {
            var count = 0;
            // check other cols.
            for (col in newCols) {
                if (col[count++] != newCols[0]) {
                    isValidDirection = false;
                }
            }
            direction = 1; // down
        }
        if (direction != -1) {
            var directionElement = document.getElementById('direction');
            directionElement.value = direction;
        }

        // Check for valid play direction.
        if (direction == -1) {
            //alert("All played tiles must be in a single row or column.");
            return false;
        }

        var colElement = document.getElementById('col');
        var rowElement = document.getElementById('row');

        if (isFirstMove()) {
            // Check that the play includes the center square.
            if (newCellIds.indexOf(CenterSquare) == -1) {
                return false;
            }
        } else {
            // Check that the play is adjacent to existing tiles.
            var row = parseInt(rowElement.value);
            var col = parseInt(colElement.value);
            var adjacent = false;
            if (direction == 2) {
                if (isAdjacent(row, col)) {
                    adjacent = true;
                }
            } else if (direction == 0) {
                // Direction is across.
                var done = false;
                var adjacent = false;
                var count = 0;
                while (!done) {
                    if (isAdjacent(row, col)) {
                        adjacent = true;
                        done = true;
                    }

                    if (isPlayedTile(row, col)) {
                        count++;
                    }
                    col++;
                    if (col > BoardSize || count > playedTiles.length) {
                        done = true;
                    }
                }
            } else {
                // Direction is down.
                var done = false;
                var adjacent = false;
                var count = 0;
                while (!done) {
                    if (isAdjacent(row, col)) {
                        adjacent = true;
                        done = true;
                    }

                    if (isPlayedTile(row, col)) {
                        count++;
                    }
                    row++;
                    if (row > BoardSize || count >= playedTiles.length) {
                        done = true;
                    }
                }
            }

            if (adjacent == false) {
                return false;
            }
        }

        // Check that the play is contiguous (no gaps between first and last tile played).
        var row = parseInt(rowElement.value);
        var col = parseInt(colElement.value);
        if (direction == 0) {
            // Direction is across.
            var done = false;
            var count = 0;
            while (!done) {

                if (isPlayedTile(row, col)) {
                    count++;
                }
                else {
                    if (isEmptySpace(row, col)) {
                        if (count == playedTiles.length) {
                            done = true;
                        }
                        else {
                            return false;
                        }
                    }
                }
                col++;
                if (col > BoardSize || count >= playedTiles.length) {
                    done = true;
                }
            }
        } else if (direction == 1) {
            // Direction is down.
            var done = false;
            var count = 0;
            while (!done) {

                if (isPlayedTile(row, col)) {
                    count++;
                }
                else {
                    if (isEmptySpace(row, col)) {
                        if (count == playedTiles.length) {
                            done = true;
                        }
                        else {
                            // A gap was found between the first and last played tile.
                            return false;
                        }
                    }
                }
                row++;
                if (row > BoardSize || count >= playedTiles.length) {
                    done = true;
                }
            }
        }
        return true;
    }

    // Submits the current play to the server.
    // Called when the user pressed the Submit Play button.
    function capturePlayDetails(event) {
        var tiles = new String()
        for (var index = 0; index < playedCharacters.length; index++) {
            tiles = tiles.concat(playedCharacters[index]);
        }
        var tilesElement = document.getElementById('tiles');
        tilesElement.value = tiles;

        // Get the remaining tiles in the rack.
        var rackElements = document.getElementsByClassName('rackTile');
        var tilesRemaining = new String()
        for (var index = 0; index < rackElements.length ; index++) {
            var element = rackElements[index];
            var tileLetter = element.getAttribute('alt').substr(0, 1);
            tilesRemaining = tilesRemaining.concat(tileLetter);
        }
        var tilesRemainingElement = document.getElementById('remainingTiles');
        tilesRemainingElement.value = tilesRemaining;

        var colElement = document.getElementById('col');
        var rowElement = document.getElementById('row');
        var scoreElement = document.getElementById('scoreForPlay');
        var scoreForPlay = parseInt(scoreElement.textContent.substr(2));

        var directionElement = document.getElementById('direction');
        var direction = directionElement.value;

        //alert("Confirm Play. \nROW: " + rowElement.value + " COL: " + colElement.value + " \n" + "Tiles: " + tiles + " Remaining Tiles: " + tilesRemaining + "\n Direction: " + direction + "\n Score: " + scoreForPlay);

        return true;


    }

    // This is the event handler for "ondragover" event for board squares and tile rack spaces.
    function allowDrop(event) {
        // Disable the default behavior which is not to allow a drop.
        event.preventDefault()
    }

    // Capture the id of the tile being dragged. This is the
    // event handler for the "ondragstart" event.
    function dragItem(event) {
        event.dataTransfer.setData("Text", event.target.id);
    }

    // When a tile is dropped, add it to the board or the rack, depending on where it was dropped.
    // This is the event handler for the "ondrop" event.
    function dropItem(event) {
        event.preventDefault();
        var itemId = event.dataTransfer.getData("Text");
        var draggedItem = document.getElementById(itemId);
        var oldParent = draggedItem.parentNode;
        var newParent = event.target.parentNode;
        var wasFromBoard = false;

        // Restore the original board square if the item was moved from somewhere on the board
        if (oldParent != null && oldParent.getAttribute("class") == "boardSpace") {
            wasFromBoard = true;
            var childImageNodes = oldParent.getElementsByClassName("spaceimage");
            if (childImageNodes.length > 0) {
                childImageNodes[0].style.display = "block";
            }
        }

        // If the tile is being dropped on an empty rack space,
        // reset the class name since this is now a rack tile.
        if (event.target.className == "rackSpace") {
            draggedItem.className = "rackTile";
            // Forget what letter any blanks were set to
            var letterChar = draggedItem.getAttribute('alt');
            if (letterChar.substr(0, 1) == '_') {
                draggedItem.setAttribute('alt', "_0");
                var tileImageFilename = "/images/tiles/Tile__.png";
                draggedItem.setAttribute('src', tileImageFilename);
            }
            event.target.appendChild(draggedItem);
            if (wasFromBoard) {
                updateBoard();
            }
            return;
        }
            // or if the tile is dropped on top of an existing tile in the rack
        else if (newParent != null && newParent.className == "rackSpace") {
            // reset the class name since this is now a rack tile
            draggedItem.className = "rackTile";
            // Forget what letter any blanks were set to
            var letterChar = draggedItem.getAttribute('alt');
            if (letterChar.substr(0, 1) == '_') {
                draggedItem.setAttribute('alt', "_0");
                var tileImageFilename = "/images/tiles/Tile__.png";
                draggedItem.setAttribute('src', tileImageFilename);
            }
            // insert the dragged element in the rack
            newParent.insertBefore(draggedItem, event.target);
            if (wasFromBoard) {
                updateBoard();
            }

            return;
        } else if (oldParent == newParent) {
            // The tile was dropped back where it started, so don't do anything.
            return;
        } else {
            // The tile was dropped on the board
            var boardSpaceNode = event.target;
            draggedItem.className = "playedTile";
            //parentNode.replaceChild(document.getElementById(item), nodeToRemove);
            boardSpaceNode.style.display = "none";
            newParent.appendChild(draggedItem);
            //event.target.appendChild(document.getElementById(item));

            // Check to see if the item dropped was a blank tile
            var success = false;
            while (!success) {
                var letterChar = draggedItem.getAttribute('alt');
                if (letterChar.substr(0, 1) == '_') {
                    var input = window.prompt("Enter a letter for the blank.");
                    if (input.length > 0) {
                        var letter = input.charAt(0).toLowerCase()
                        if (letter >= 'a' && letter <= 'z') {
                            draggedItem.setAttribute('alt', letter.concat("0"));
                            var tileImageFilename = "/images/tiles/Tile_" + letter.toUpperCase() + "_Blank.png";
                            draggedItem.setAttribute("src", tileImageFilename);
                            success = true;
                        }
                        else {
                            alert("Enter a letter.");
                        }
                    } else {
                        alert("Enter a letter.")
                    }
                }
                else {
                    success = true;
                }
            }
        }
        updateBoard();
    }

    // Called when a user swaps tiles and selects a tile that is not in the rack.
    function noTileError() {
    
    }

    // 
    function userHasTile(ch) {
        // get the tiles in rack
        var rackElements = document.getElementsByClassName('rackTile');
        var tilesRemaining = new String();
        for (var index = 0; index < rackElements.length ; index++) {
            var node = rackElements[index];
            var tileLetter = node.innerText;
            tilesRemaining = tilesRemaining.concat(tileLetter);
        }
        if (tilesRemaining.indexOf(ch) != -1) {
            return true;
        }
        return false;
    }

    // Display a dialog asking which tiles the user wants to swap and then
    // submit that to the server.
    // Event handler for the onclick event for the Swap Tiles button.
    function swapTiles() {
        var result = window.prompt("Enter the tiles you want to swap:");
        if (result == null) {
            return;
        }
        var tilesToSwap = "";
        for (var index = 0; index < result.length; index++) {
            if (result[index] == '_' || result[index] == ' ') {
                if (userHasTile('_')) {
                    tilesToSwap = tilesToSwap.concat('_');
                }
                else {
                    alert("You do not have those tiles to swap.");
                    return;
                }
            
            }
            else {
                var ch = result[index].toUpperCase();
                if (userHasTile(ch)) {
                    tilesToSwap = tilesToSwap.concat(ch);
                }
                else {
                    alert("You do not have those tiles to swap.");
                    return;
                }
            }
        }
        var answer = window.confirm("Swap the following tiles? \n " + tilesToSwap);
        if (answer) {
            var gameIDElement = document.getElementById('gameID');
            var playerIDElement = document.getElementById('playerID');
            window.location = "Swap?gameId=" + gameIDElement.value + "&playerId=" + playerIDElement.value + "&tilesToSwap=" + tilesToSwap;
        }

    }


