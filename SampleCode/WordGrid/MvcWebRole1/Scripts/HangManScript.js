// HangManScript.js
// This file contains the client-side script for the hangman word game.

// This code does not use any non-default Javascript libraries.

document.onkeyup = OnKey
document.onload = OnLoad

function OnLoad() {
    
}

function selectLetter(id) {
    var oldSelected = document.getElementsByClassName('rackTileSelected')
    if (oldSelected.length > 0) {
        oldSelected[0].style.outline = '0'
        oldSelected[0].className = 'rackTile'
    }

    var myTurnElement = document.getElementById("myturn");
    var submitButton = document.getElementById("submitPlay");
    var myTurn = myTurnElement != null;

    if (myTurn)
    {
        var newSelected = document.getElementById(id)
        console.log("new selected id is: " + newSelected.id)
        newSelected.className = 'rackTileSelected'
        newSelected.style.outline = '2px solid blue'

        myTurnElement.innerText = "YOUR TURN!"; // this is redundant, but whatever.
        submitButton.disabled = false;
    } else
    {
        submitButton.disabled = true;
    }
}

function OnKey(e) {
    
}

// Update the in-memory representation of the game.
// Called when the board is changed (a new tile is added, for example).
function updateBoard() {

    var myTurnElement = document.getElementById("myturn");
    var submitButton = document.getElementById("submitPlay");

    // Check to see if the current configuration forms a valid play.
    // If the turn element is null, then it's not the current user's turn.
    if (myTurnElement != null && document.getElementsByClassName('rackTileSelected').length > 0) {
        myTurnElement.innerText = "YOUR TURN!";
        submitButton.disabled = false;
    } else {
        myTurnElement.innerText = "NOT YOUR TURN!";
        submitButton.disabled = true;
    }
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

  




