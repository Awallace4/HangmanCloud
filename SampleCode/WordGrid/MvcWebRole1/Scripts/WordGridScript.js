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

document.onkeyup = OnKey
document.onload = OnLoad


function convertToChar(keyID) {
    //var chars = ["A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M", "N", "O", "P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z" ]
    var chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    return chars.charAt(keyID - 65)
}

function OnLoad() {
    setCurrentCellBorder()
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

function selectLetter(id) {
    var oldSelected = document.getElementsByClassName('rackTileSelected')
    if (oldSelected.length > 0) {
        oldSelected[0].style.outline = '0'
        oldSelected[0].className = 'rackTile'
    }

    var newSelected = document.getElementById(id)
    console.log("new selected id is: " + newSelected.id)
    newSelected.className = 'rackTileSelected'
    newSelected.style.outline = '2px solid blue'

    var submitButton = document.getElementById('submitPlay')
    submitButton.disabled = false;
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

    // Update the in-memory representation of the board.
    // Called when the board is changed (a new tile is added, for example).
    function updateBoard() {

        var myTurnElement = document.getElementById("myturn");
        var submitButton = document.getElementById("submitPlay");

        // Check to see if the current configuration forms a valid play.
        // If the score element is null, then it's not the user's turn.
        if (myTurnElement != null && document.getElementsByClassName('rackTileSelected').length > 0) {
            myTurnElement.innerText = "YOUR TURN!";
            submitButton.disabled = false;
        } else {
            myTurnElement.innerText = "";
            submitButton.disabled = true;
        }

    }

    // Returns true if this is the first move of a game, which means that
    // the user must include the center square in the play.
    function isFirstMove() {
        // It is the first move if there are no board tiles.
        // TODO: rewrite me for hangman
        return oldCellIds.length == 0;
    }

    // Determines whether a play on the board is a legal move
    // A play is legal if at least one tile is adjacent
    // all tiles are in a single row or column
    // and the played and unplayed tiles are contiguous (have no gaps).
    function isValidPlay() {

        // TODO: rewrite me for hangman!
        return true;
    }

    // Submits the current play to the server.
    // Called when the user pressed the Submit Play button.
    function capturePlayDetails(event) {

        var selectedLetterElem = document.getElementsByClassName('rackTileSelected')[0];
        var guessedLetter = selectedLetterElem.id.substring(6, 7);
        var guessedLetterElem = document.getElementById('guessedLetter');
        guessedLetterElem.value = guessedLetter;


        console.log("Confirm Play. \nSELECTED LETTER: " + guessedLetterElem.value);

        return true;


    }

   

    // Called when a user swaps tiles and selects a tile that is not in the rack.
    function noTileError() {
    
    }




