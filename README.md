# Mail.Ru-.net-cloud-client
.Net Client for cloud.mail.ru

Main functionality with implementations:
- Create folder
  CreateFolder(string folderName, string destinationPath) - return bool result of the operation
- Copy file
  <br/>Copy(File fileObject, Entry destinationEntryObject)  - return bool result of the operation
  <br/>Copy(File fileObject, Folder destinationFolderObject) - return bool result of the operation
  <br/>Copy(File fileObject, string destinationPath) - return bool result of the operation
- Copy folder
  Copy(Folder folderObject, Entry destinationEntryObject) - return bool result of the operation
  Copy(Folder folderObject, Folder destinationFolderObject) - return bool result of the operation
  Copy(Folder folderObject, string destinationPath) - return bool result of the operation
- Download file
  GetFile(File fileObject, [bool includeProgressEvent = True]) - return byte array of the file
  GetFile(File fileObject, string destinationPathOnCopmuter, [bool includeProgressEvent = True]) - return bool result of the operation and save file in destination path on computer
- Upload file
  UploadFile(FileInfo file, string destinationPath) - return bool result of the operation
- Get list of the files and folders
  GetItems(Folder folderObject) - return Entry object with files and folders on the server (include list of the File and Folder object)
  GetItems(string pathOnServer) - return Entry object with files and folders on the server (include list of the File and Folder object)
- Get public file link (not support for large files)
  GetPublishLink(File fileObject) - return public file URL as string
- Get public folder link
  GetPublishLink(Folder folderObject) - return public folder URL as string
- Get direct file link (operation on one session)
  GetPublishDirectLink(string publicFileLink, FileType fileType) - return direct file URL as string
- Move file
  Move(File fileObject, Entry destinationEntryObject)  - return bool result of the operation
  Move(File fileObject, Folder destinationFolderObject) - return bool result of the operation
  Move(File fileObject, string destinationPath) - return bool result of the operation
- Move folder
  Move(Folder folderObject, Entry destinationEntryObject) - return bool result of the operation
  Move(Folder folderObject, Folder destinationFolderObject) - return bool result of the operation
  Move(Folder folderObject, string destinationPath) - return bool result of the operation
- Rename file
  Rename(File fileObject, string newFileName) - return bool result of the operation
- Rename folder
  Rename(Folder folderObject, string newFolderName) - return bool result of the operation
- Remove file
  Remove(File fileObject) - return bool result of the operation
- Remove folder
  Remove(Folder folderObject) - return bool result of the operation
- Disable public file link
  UnpublishLink(File fileObject) - return bool result of the operation
- Disable public folder link
  UnpublishLink(Folder folderObject) - return bool result of the operation
- Cancel all async threads
  AbortAllAsyncThreads() - return nothing, just remove all async operations to cloud

All operations supports async calls.
Upload and Download operations supports progress change event and can work with large file more than 2Gb.

Mail.ru cloud paths start with symbol "/", e.g:
- Root directory just start with "/"
- Folder in root directory as "/New folder"
- File in root directory as "/NewFileName.txt"

--------------------------------------------------
              VERY BETA VERSION
--------------------------------------------------

Distributed under the MIT license
