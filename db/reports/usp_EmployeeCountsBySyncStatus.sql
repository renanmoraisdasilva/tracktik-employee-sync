-- Bonus stored procedure: employee counts grouped by sync status.
-- Run after the initial migration has created dbo.Employees.

CREATE OR ALTER PROCEDURE [dbo].[usp_EmployeeCountsBySyncStatus]
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        [SyncStatus],
        COUNT(*) AS [EmployeeCount]
    FROM [dbo].[Employees]
    GROUP BY [SyncStatus]
    ORDER BY [SyncStatus];
END;
