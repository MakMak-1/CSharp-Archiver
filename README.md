# Archiver

## Overview
The application is designed to compress files and directories to save disk space and optimize data exchange.

The project aims to achieve a compression ratio of at least **75%** for standard office and text files while maintaining an archiving speed of approximately **5 seconds per 100 MB** of data.

## Key Features
* **Multiple Formats**: Support for creating and managing archives in `.zip` and `.7z` formats.
* **Compression Control**: Users can choose between various compression levels, including Store, Fast, Normal, and Best.
* **Security**: Support for archive password protection and data encryption using the **AES-256** standard.
* **Archive Modification**: Capability to view, add, or delete files within an existing archive without full extraction.
* **File System Navigation**: Integrated file system browser for intuitive selection of files and folders.

## Tech Stack
* **Language**: C# 12.0
* **Platform**: .NET 9.0
* **Framework**: Windows Presentation Foundation (WPF)
* **Architecture**: Monolithic design following the **C4 model**, separated into a Business Logic Layer (BLL) and a Presentation Layer (GUI).
* **Libraries**: 
    * **DotNetZip**: For `.zip` format operations.
    * **SevenZipSharp**: A wrapper for the native `7z.dll` to handle `.7z` archives.
