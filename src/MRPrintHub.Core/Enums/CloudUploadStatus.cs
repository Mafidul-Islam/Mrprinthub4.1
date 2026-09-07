namespace MRPrintHub.Core.Enums;

/// <summary>
/// Lifecycle status of an online file upload in the MR Print Hub Cloud.
/// </summary>
public enum CloudUploadStatus
{
    /// <summary>
    /// Upload session initialized and awaiting file transfer.
    /// </summary>
    Created,

    /// <summary>
    /// File is actively uploading from customer mobile to Cloud.
    /// </summary>
    Uploading,

    /// <summary>
    /// File completely uploaded to Cloud temporary storage; awaiting delivery.
    /// </summary>
    Uploaded,

    /// <summary>
    /// Notification dispatched to Desktop; file download in progress.
    /// </summary>
    Delivering,

    /// <summary>
    /// File successfully downloaded and acknowledged by Desktop; Cloud temp file deleted.
    /// </summary>
    Delivered,

    /// <summary>
    /// Retention period expired before Desktop downloaded the file.
    /// </summary>
    Expired,

    /// <summary>
    /// File upload or delivery failed due to validation or network errors.
    /// </summary>
    Failed
}
