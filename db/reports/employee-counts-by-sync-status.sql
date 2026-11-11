-- Bonus report: employee counts grouped by sync status.
-- Read-only. The API exposes the same shape at
-- GET /api/v1/reports/employees-by-sync-status.

SELECT
    [SyncStatus],
    COUNT(*) AS [EmployeeCount]
FROM [dbo].[Employees]
GROUP BY [SyncStatus]
ORDER BY [SyncStatus];
