namespace WorkerRole1

open System
open System.Collections.Generic
open System.Diagnostics
open System.Net
open System.Threading
open Microsoft.WindowsAzure
open Microsoft.WindowsAzure.Diagnostics
open Microsoft.WindowsAzure.ServiceRuntime
open Microsoft.WindowsAzure.StorageClient
open FsAzureHelper.FsAzureHelpers


module DictionaryInfo =
    let dictionaryFilename = "dictionary"


// This worker role handles generating a random word for the hangman game. When a game begins,
// a message is submitted to a queue "generate" containing an id.
// The worker returns a message containing the same id an a random word from the dictionary.
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
    let blobContainer = blobClient.GetContainerReference("hangman")
     
    let queueClient = storageAccount.CreateCloudQueueClient()
    
    // An in-memory representation of the word list that's used in this game.
    let numWords = 51
    let dictionary =
            blobContainer.CreateIfNotExist()
            let blob = blobContainer.GetBlobReference(DictionaryInfo.dictionaryFilename).ToBlockBlob
            
            //For offline debugging
            //Must manually set path to dictionary.txt
            use fileStream = System.IO.File.OpenRead("C:\Users\Daniel\Dropbox\Class_Files\CS_491\HangmanCloud\SampleCode\WordGrid\WorkerRole1\dictionary.txt")
            blob.UploadFromStream(fileStream)
            fileStream.Close()
            let blobString = blob.DownloadText()
            blobString.Split([|'\r'; '\n'|], StringSplitOptions.RemoveEmptyEntries)
            
    // select a random word from dictionary
    member this.GetRandomWord() =
        let random = System.Random()
        let index = random.Next(0, numWords)
        dictionary.[index]

    // This is the main message processing loop for the F# worker role.
    override this.Run() =
        log "WorkerRole1 entry point called" "Information"
        // queue for generating random word from dictionary
        let generateQueue = queueClient.GetQueueReference("generate")
        // queue in which to put chosen words
        let resultQueue = queueClient.GetQueueReference("results")

        generateQueue.CreateIfNotExist() |> ignore
        resultQueue.CreateIfNotExist() |> ignore

        while(true) do 
            let message = generateQueue.GetMessage()
            if (message = null) then
                Thread.Sleep(1000)
            else
                generateQueue.DeleteMessage(message)
                // messages contain a token (ID) on the first line
                // the return message contains the same token (ID) and
                // a random word from the dictionary
                let messageString = message.AsString
                let buildMessage id =
                    let body = this.GetRandomWord() + "\n"
                    String.Concat(id + "\n", body)
                let newMessage =
                    match (messageString.Split([| '\n' |]) |> Array.toList) with
                    | head :: tail -> new CloudQueueMessage(buildMessage head)
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
