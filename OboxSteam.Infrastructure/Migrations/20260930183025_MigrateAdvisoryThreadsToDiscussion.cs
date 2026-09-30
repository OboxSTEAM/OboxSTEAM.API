using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OboxSteam.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MigrateAdvisoryThreadsToDiscussion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Copies General-thread messages and the root message of every RequiredChange /
            // Suggestion thread into the program chat, interleaved by original CreatedAt and
            // appended after existing chat messages. Open / Addressed RequiredChange roots become
            // pins; everything else is a plain message. Thread tables are left untouched.
            migrationBuilder.Sql("""
                CREATE TEMP TABLE advisory_migration_source AS
                WITH general_messages AS (
                    SELECT m."Id" AS "SourceId", t."ProgramId", m."AuthorUserId", m."Message" AS "Body",
                           m."CreatedAt", t."Id" AS "ThreadId", t."Type", t."Status",
                           t."TargetType", t."TargetId", t."TargetLabel",
                           t."AuthorUserId" AS "ThreadAuthorUserId", t."CreatedAt" AS "ThreadCreatedAt",
                           t."UpdatedAt" AS "ThreadUpdatedAt", t."LastMessageAt"
                    FROM "ProgramAdvisoryMessages" m
                    JOIN "ProgramAdvisoryThreads" t ON t."Id" = m."ThreadId"
                    WHERE m."IsDeleted" = false AND t."IsDeleted" = false AND t."Type" = 'General'
                ),
                root_messages AS (
                    SELECT DISTINCT ON (m."ThreadId")
                           m."Id" AS "SourceId", t."ProgramId", m."AuthorUserId", m."Message" AS "Body",
                           m."CreatedAt", t."Id" AS "ThreadId", t."Type", t."Status",
                           t."TargetType", t."TargetId", t."TargetLabel",
                           t."AuthorUserId" AS "ThreadAuthorUserId", t."CreatedAt" AS "ThreadCreatedAt",
                           t."UpdatedAt" AS "ThreadUpdatedAt", t."LastMessageAt"
                    FROM "ProgramAdvisoryMessages" m
                    JOIN "ProgramAdvisoryThreads" t ON t."Id" = m."ThreadId"
                    WHERE m."IsDeleted" = false AND t."IsDeleted" = false
                      AND t."Type" IN ('RequiredChange', 'Suggestion')
                    ORDER BY m."ThreadId", m."StreamSequence", m."CreatedAt", m."Id"
                ),
                source AS (
                    SELECT * FROM general_messages
                    UNION ALL
                    SELECT * FROM root_messages
                ),
                resolved AS (
                    SELECT s.*,
                           CASE WHEN s."TargetType" = 'Program' THEN s."ProgramId" ELSE s."TargetId" END AS "MentionTargetId",
                           CASE WHEN s."Type" = 'RequiredChange' AND s."Status" IN ('Open', 'Addressed')
                                THEN s."Status" END AS "PinStatus"
                    FROM source s
                ),
                checked AS (
                    SELECT r.*,
                           (r."Type" <> 'General' AND r."MentionTargetId" IS NOT NULL AND CASE r."TargetType"
                               WHEN 'Program' THEN EXISTS (SELECT 1 FROM "Programs" x WHERE x."Id" = r."MentionTargetId" AND x."IsDeleted" = false)
                               WHEN 'Module' THEN EXISTS (SELECT 1 FROM "Modules" x WHERE x."Id" = r."MentionTargetId" AND x."IsDeleted" = false)
                               WHEN 'Course' THEN EXISTS (SELECT 1 FROM "Courses" x WHERE x."Id" = r."MentionTargetId" AND x."IsDeleted" = false)
                               WHEN 'Activity' THEN EXISTS (SELECT 1 FROM "Activities" x WHERE x."Id" = r."MentionTargetId" AND x."IsDeleted" = false)
                               WHEN 'Assignment' THEN EXISTS (SELECT 1 FROM "Assignments" x WHERE x."Id" = r."MentionTargetId" AND x."IsDeleted" = false)
                               WHEN 'ResearchMilestone' THEN EXISTS (SELECT 1 FROM "ResearchMilestones" x WHERE x."Id" = r."MentionTargetId" AND x."IsDeleted" = false)
                               WHEN 'Material' THEN EXISTS (SELECT 1 FROM "Materials" x WHERE x."Id" = r."MentionTargetId" AND x."IsDeleted" = false)
                               ELSE false
                           END) AS "HasMention"
                    FROM resolved r
                ),
                base_sequence AS (
                    SELECT p."Id" AS "ProgramId",
                           GREATEST(p."AdvisoryDiscussionSequence",
                                    COALESCE((SELECT MAX(d."Sequence") FROM "ProgramAdvisoryDiscussionMessages" d
                                              WHERE d."ProgramId" = p."Id"), 0)) AS "BaseSequence"
                    FROM "Programs" p
                )
                SELECT c.*,
                       gen_random_uuid() AS "NewMessageId",
                       gen_random_uuid() AS "NewReferenceId",
                       gen_random_uuid() AS "NewLinkId",
                       b."BaseSequence" + ROW_NUMBER() OVER (PARTITION BY c."ProgramId" ORDER BY c."CreatedAt", c."SourceId") AS "NewSequence",
                       LEFT(
                           CASE
                               WHEN c."HasMention" THEN '@[' || c."TargetType" || ':' || c."MentionTargetId"::text || '] '
                               WHEN c."Type" <> 'General' AND c."TargetType" <> 'RubricCriterion' THEN '[' || c."TargetLabel" || '] '
                               ELSE ''
                           END || c."Body", 10000) AS "NewText"
                FROM checked c
                JOIN base_sequence b ON b."ProgramId" = c."ProgramId"
                WHERE NOT EXISTS (
                    SELECT 1 FROM "ProgramAdvisoryDiscussionMessages" d
                    WHERE d."ProgramId" = c."ProgramId"
                      AND d."ClientMessageId" = 'migrated:' || c."SourceId"::text
                      AND d."IsDeleted" = false);

                INSERT INTO "ProgramAdvisoryDiscussionMessages" (
                    "Id", "ProgramId", "Sequence", "AuthorUserId", "ClientMessageId", "Text", "Kind",
                    "PinStatus", "PinnedAt", "PinnedByUserId", "AddressedAt",
                    "IsDeleted", "CreatedAt", "CreatedBy")
                SELECT s."NewMessageId", s."ProgramId", s."NewSequence", s."AuthorUserId",
                       'migrated:' || s."SourceId"::text, s."NewText", 'User',
                       s."PinStatus",
                       CASE WHEN s."PinStatus" IS NOT NULL THEN s."ThreadCreatedAt" END,
                       CASE WHEN s."PinStatus" IS NOT NULL THEN s."ThreadAuthorUserId" END,
                       CASE WHEN s."PinStatus" = 'Addressed' THEN COALESCE(s."ThreadUpdatedAt", s."LastMessageAt") END,
                       false, s."CreatedAt", s."AuthorUserId"
                FROM advisory_migration_source s;

                INSERT INTO "ProgramAdvisoryReferences" (
                    "Id", "ProgramId", "Context", "SubmissionId", "TargetType", "TargetId", "AnchorKind",
                    "CapturedLabel", "CapturedExcerpt", "CapturedAt", "IsDeleted", "CreatedAt", "CreatedBy")
                SELECT s."NewReferenceId", s."ProgramId", 'WorkingDraft', NULL, s."TargetType", s."MentionTargetId", 'Node',
                       LEFT(s."TargetLabel", 255), NULL, s."CreatedAt", false, s."CreatedAt", s."AuthorUserId"
                FROM advisory_migration_source s
                WHERE s."HasMention";

                INSERT INTO "ProgramAdvisoryDiscussionMessageReferences" (
                    "Id", "MessageId", "ReferenceId", "Ordinal", "IsDeleted", "CreatedAt", "CreatedBy")
                SELECT s."NewLinkId", s."NewMessageId", s."NewReferenceId", 0, false, s."CreatedAt", s."AuthorUserId"
                FROM advisory_migration_source s
                WHERE s."HasMention";

                UPDATE "Programs" p
                SET "AdvisoryDiscussionSequence" = m."MaxSequence"
                FROM (SELECT "ProgramId", MAX("NewSequence") AS "MaxSequence"
                      FROM advisory_migration_source GROUP BY "ProgramId") m
                WHERE p."Id" = m."ProgramId" AND p."AdvisoryDiscussionSequence" < m."MaxSequence";

                DROP TABLE advisory_migration_source;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
