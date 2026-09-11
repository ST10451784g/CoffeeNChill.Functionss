using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using CoffeeNChill.Functions.Services;

namespace CoffeeNChill.Functions
{
    public class DocumentFunctions
    {
        private readonly ILogger _logger;
        private readonly BlobStorageService _blobStorageService;

        public DocumentFunctions(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<DocumentFunctions>();
            _blobStorageService = new BlobStorageService();
        }

        // ==========================================
        // UPLOAD STAFF DOCUMENT
        // POST /api/documents/upload
        // ==========================================

        [Function("UploadStaffDocument")]
        public async Task<HttpResponseData> UploadStaffDocument(
            [HttpTrigger(
                AuthorizationLevel.Anonymous,
                "post",
                Route = "documents/upload")]
            HttpRequestData req)
        {
            _logger.LogInformation("Uploading staff document.");

            try
            {
                // Check Content-Type
                if (!req.Headers.TryGetValues(
                        "Content-Type",
                        out var contentTypes))
                {
                    var badResponse =
                        req.CreateResponse(HttpStatusCode.BadRequest);

                    await badResponse.WriteStringAsync(
                        "Content-Type is required.");

                    return badResponse;
                }

                string contentType =
                    contentTypes.FirstOrDefault() ?? string.Empty;

                // Check multipart/form-data
                if (!contentType.StartsWith(
                        "multipart/form-data",
                        StringComparison.OrdinalIgnoreCase))
                {
                    var badResponse =
                        req.CreateResponse(HttpStatusCode.BadRequest);

                    await badResponse.WriteStringAsync(
                        "Request must be multipart/form-data.");

                    return badResponse;
                }

                // Get multipart boundary
                string? boundary = GetBoundary(contentType);

                if (string.IsNullOrWhiteSpace(boundary))
                {
                    var badResponse =
                        req.CreateResponse(HttpStatusCode.BadRequest);

                    await badResponse.WriteStringAsync(
                        "Multipart boundary is missing.");

                    return badResponse;
                }

                var reader = new MultipartReader(
                    boundary,
                    req.Body);

                MultipartSection? section;

                while ((section = await reader.ReadNextSectionAsync())
                       != null)
                {
                    // Look for uploaded file
                    if (!section.Headers.TryGetValue(
                            "Content-Disposition",
                            out var contentDisposition))
                    {
                        continue;
                    }

                    string disposition =
                        contentDisposition.ToString();

                    if (!disposition.Contains(
                            "filename=",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string fileName =
                        GetFileName(disposition);

                    if (string.IsNullOrWhiteSpace(fileName))
                    {
                        var badResponse =
                            req.CreateResponse(
                                HttpStatusCode.BadRequest);

                        await badResponse.WriteStringAsync(
                            "File name is required.");

                        return badResponse;
                    }

                    // Only PDF files are allowed
                    if (!fileName.EndsWith(
                            ".pdf",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        var badResponse =
                            req.CreateResponse(
                                HttpStatusCode.BadRequest);

                        await badResponse.WriteStringAsync(
                            "Only PDF files are allowed.");

                        return badResponse;
                    }

                    // Remove unsafe path characters
                    fileName = Path.GetFileName(fileName);

                    BlobContainerClient containerClient =
                        _blobStorageService.GetContainerClient();

                    BlobClient blobClient =
                        containerClient.GetBlobClient(fileName);

                    // Stream file directly into Blob Storage
                    await using Stream blobStream =
                        await blobClient.OpenWriteAsync(
                            overwrite: true);

                    await section.Body.CopyToAsync(blobStream);

                    await blobStream.FlushAsync();

                    _logger.LogInformation(
                        "Successfully uploaded {FileName}.",
                        fileName);

                    var successResponse =
                        req.CreateResponse(HttpStatusCode.OK);

                    await successResponse.WriteStringAsync(
                        $"Document '{fileName}' uploaded successfully.");

                    return successResponse;
                }

                var noFileResponse =
                    req.CreateResponse(HttpStatusCode.BadRequest);

                await noFileResponse.WriteStringAsync(
                    "No file was provided.");

                return noFileResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error uploading staff document.");

                var errorResponse =
                    req.CreateResponse(
                        HttpStatusCode.InternalServerError);

                await errorResponse.WriteStringAsync(
                    "An error occurred while uploading the document.");

                return errorResponse;
            }
        }


        // ==========================================
        // LIST STAFF DOCUMENTS
        // GET /api/documents
        // ==========================================

        [Function("ListStaffDocuments")]
        public async Task<HttpResponseData> ListStaffDocuments(
            [HttpTrigger(
                AuthorizationLevel.Anonymous,
                "get",
                Route = "documents")]
            HttpRequestData req)
        {
            _logger.LogInformation(
                "Listing staff documents.");

            try
            {
                BlobContainerClient containerClient =
                    _blobStorageService.GetContainerClient();

                var documents = new List<object>();

                await foreach (
                    var blobItem in containerClient.GetBlobsAsync())
                {
                    documents.Add(new
                    {
                        fileName = blobItem.Name,

                        size =
                            blobItem.Properties.ContentLength ?? 0,

                        lastModified =
                            blobItem.Properties.LastModified
                    });
                }

                var response =
                    req.CreateResponse(HttpStatusCode.OK);

                await response.WriteAsJsonAsync(documents);

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error listing staff documents.");

                var errorResponse =
                    req.CreateResponse(
                        HttpStatusCode.InternalServerError);

                await errorResponse.WriteStringAsync(
                    "An error occurred while listing documents.");

                return errorResponse;
            }
        }


        // ==========================================
        // GET MULTIPART BOUNDARY
        // ==========================================

        private static string? GetBoundary(
            string contentType)
        {
            const string boundaryKey = "boundary=";

            int boundaryIndex =
                contentType.IndexOf(
                    boundaryKey,
                    StringComparison.OrdinalIgnoreCase);

            if (boundaryIndex < 0)
            {
                return null;
            }

            string boundary =
                contentType.Substring(
                    boundaryIndex + boundaryKey.Length)
                .Trim();

            if (boundary.StartsWith("\"") &&
                boundary.EndsWith("\""))
            {
                boundary =
                    boundary.Substring(
                        1,
                        boundary.Length - 2);
            }

            return boundary;
        }


        // ==========================================
        // GET FILE NAME
        // ==========================================

        private static string GetFileName(
            string contentDisposition)
        {
            const string fileNameKey = "filename=";

            int fileNameIndex =
                contentDisposition.IndexOf(
                    fileNameKey,
                    StringComparison.OrdinalIgnoreCase);

            if (fileNameIndex < 0)
            {
                return string.Empty;
            }

            string fileName =
                contentDisposition.Substring(
                    fileNameIndex + fileNameKey.Length)
                .Trim();

            int semicolonIndex =
                fileName.IndexOf(';');

            if (semicolonIndex >= 0)
            {
                fileName =
                    fileName.Substring(
                        0,
                        semicolonIndex);
            }

            fileName =
                fileName.Trim('"');

            return fileName;

        }

        // DOWNLOAD STAFF DOCUMENT
        [Function("DownloadStaffDocument")]
        public async Task<HttpResponseData> DownloadStaffDocument(
            [HttpTrigger(
        AuthorizationLevel.Anonymous,
        "get",
        Route = "documents/download/{fileName}")]
    HttpRequestData req,
            string fileName)
        {
            _logger.LogInformation(
                "Downloading staff document: {FileName}",
                fileName);

            try
            {
                if (string.IsNullOrWhiteSpace(fileName))
                {
                    var badResponse =
                        req.CreateResponse(HttpStatusCode.BadRequest);

                    await badResponse.WriteStringAsync(
                        "File name is required.");

                    return badResponse;
                }

                fileName = Path.GetFileName(fileName);

                BlobContainerClient containerClient =
                    _blobStorageService.GetContainerClient();

                BlobClient blobClient =
                    containerClient.GetBlobClient(fileName);

                if (!await blobClient.ExistsAsync())
                {
                    var notFoundResponse =
                        req.CreateResponse(HttpStatusCode.NotFound);

                    await notFoundResponse.WriteStringAsync(
                        "Document not found.");

                    return notFoundResponse;
                }

                var response =
                    req.CreateResponse(HttpStatusCode.OK);

                response.Headers.Add(
                    "Content-Type",
                    "application/pdf");

                await using Stream blobStream =
                    await blobClient.OpenReadAsync();

                await blobStream.CopyToAsync(response.Body);

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error downloading staff document.");

                var errorResponse =
                    req.CreateResponse(
                        HttpStatusCode.InternalServerError);

                await errorResponse.WriteStringAsync(
                    "An error occurred while downloading the document.");

                return errorResponse;
            }
        }

    }


}