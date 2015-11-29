namespace WorkerRole1

open System
open System.Collections.Generic
open System.Diagnostics
open System.Linq
open System.Net
open System.Threading
open Microsoft.WindowsAzure
open Microsoft.WindowsAzure.Diagnostics
open Microsoft.WindowsAzure.ServiceRuntime
open Microsoft.WindowsAzure.StorageClient
open FsAzureHelper.FsAzureHelpers
#if USE_MSWORD_DICTIONARY
open Microsoft.Office.Interop.Word
#endif

module DictionaryInfo =
    let dictionaryFilename = "dictionary.txt"

#if USE_MSWORD_DICTIONARY
// Includes functionality for looking up words in the Microsoft Word
// spelling dictionary.
type WordLookup() =
    inherit obj()
    let app = new Microsoft.Office.Interop.Word.ApplicationClass()
    do app.Visible <- false
    do app.AutoCorrect.ReplaceText <- false
    do app.AutoCorrect.ReplaceTextFromSpellingChecker <- false
    let refTrue = ref (box true)
    let refFalse = ref (box false)

    // Determines if a list of words is valid according to the
    // Microsoft Word spelling dictionary.
    member this.CheckWords(words) =
        // Change to lowercase here since proper nouns will be reported as
        // misspelled if they are in lowercase.
        let words = List.map (fun (word : string) -> word.ToLower()) words

        let doc1 = app.Documents.Add(Visible = refFalse)
        let text = String.concat(" ") words
        doc1.Words.First.InsertBefore(text)
        let errors = doc1.SpellingErrors
        let numError = errors.Count

        let invalidWords = seq { for error in errors do
                                     yield error.Text
                                      }
                           |> List.ofSeq
        doc1.Close(SaveChanges = refFalse)

        let isValid word = not (List.exists (fun element -> element = word) invalidWords)
        List.map (fun element -> isValid element) words
        |> List.zip (List.map (fun (word: string) -> word.ToUpper()) words)

    // Close the instance of Microsoft Word
    override this.Finalize() =
        app.Quit(refFalse)
        base.Finalize()
#endif

// This worker role handles looking up words in the dictionary. When the user makes a play,
// a message is submitted to a queue "lookup" which contains the words to lookup for that
// play. The words are looked up and a message is submitted back to the "results" queue which
// contains the information about whether each word looked up is valid.
type WorkerRole() =
    inherit RoleEntryPoint() 

    let log message kind = Trace.WriteLine(message, kind)
    do CloudStorageAccount.SetConfigurationSettingPublisher(new System.Action<_, _>(fun configName configSetter  ->
                      // Provide the configSetter with the initial value
                      configSetter.Invoke( RoleEnvironment.GetConfigurationSettingValue( configName ) ) |> ignore
                      RoleEnvironment.Changed.AddHandler( new System.EventHandler<_>(fun sender arg ->
                        arg.Changes
                        |> Seq.toList
                        |> List.filter (fun change -> change :? RoleEnvironmentConfigurationSettingChange)
                        |> List.map (fun change -> change :?> RoleEnvironmentConfigurationSettingChange)
                        |> List.filter (fun change -> change.ConfigurationSettingName = configName && 
                                                      not (configSetter.Invoke( RoleEnvironment.GetConfigurationSettingValue(configName))))
                        |> List.iter (fun change ->
                            // In this case, the change to the storage account credentials in the
                            // service configuration is significant enough that the role needs to be
                            // recycled in order to use the latest settings (for example, the 
                            // endpoint may have changed).
                            RoleEnvironment.RequestRecycle())))))
    let storageAccount = CloudStorageAccount.FromConfigurationSetting("StorageConnectionString")
    let blobClient = storageAccount.CreateCloudBlobClient()
    let blobContainer = blobClient.GetContainerReference("WordGrid")
    let queueClient = storageAccount.CreateCloudQueueClient()

#if USE_MSWORD_DICTIONARY
    let wordLookup = new WordLookup()
#endif

    // An in-memory representation of the word list that's used in this game.
    let dictionary =
            let blob = blobContainer.GetBlobReference(DictionaryInfo.dictionaryFilename)
            let blobString = blob.DownloadText()
            blobString.Split([|'\r'; '\n'|], StringSplitOptions.RemoveEmptyEntries)

    // Looks up a single word in the dictionary.
    member this.IsValidWord(word) =
        Seq.exists (fun elem -> elem = word) dictionary

    // Check the words played in a play.
    member this.CheckWords(words) =
#if USE_MSWORD_DICTIONARY
        wordLookup.CheckWords()        
#else        
        List.map (fun word -> this.IsValidWord(word)) words
        |> List.zip words
#endif

    // This is the main message processing loop for the F# worker role.
    override this.Run() =
        log "WorkerRole1 entry point called" "Information"
        let lookupQueue = queueClient.GetQueueReference("lookup")
        let resultQueue = queueClient.GetQueueReference("results")

        lookupQueue.CreateIfNotExist() |> ignore
        resultQueue.CreateIfNotExist() |> ignore

        while(true) do 
            let message = lookupQueue.GetMessage()
            if (message = null) then
                Thread.Sleep(1000)
            else
                lookupQueue.DeleteMessage(message)
                // messages contain a token (ID) on the first line
                // then a list of words to lookup for a play, one word per line
                // the return message contains the same token (ID) and
                // the list of words with a Boolean IsValid value for each one
                let messageString = message.AsString
                let buildMessage id checkResults =
                    let body = List.map (fun (word, isValid) -> String.Format("{0} {1}", word, isValid)) checkResults
                                |> String.concat "\n" 
                    String.Concat(id + "\n", body)
                let newMessage =
                    match (messageString.Split([| '\n' |]) |> Array.toList) with
                    | head :: tail -> new CloudQueueMessage(buildMessage head (this.CheckWords(tail)))
                    | [] -> new CloudQueueMessage("Unknown error in parsing a message in the lookup queue.")
                resultQueue.AddMessage(newMessage)
            log "Working" "Information"


    // Perform any initial configuration on startup.
    override this.OnStart() = 

        // Set the maximum number of concurrent connections 
        ServicePointManager.DefaultConnectionLimit <- 12
       
        // For information on handling configuration changes
        // see the MSDN topic at http://go.microsoft.com/fwlink/?LinkId=166357.

        base.OnStart()
