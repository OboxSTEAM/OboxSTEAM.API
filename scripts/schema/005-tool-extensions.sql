-- Harness v0 schema - migration 005
-- Inbound tool registry: kind-aware presence + capability binding.

ALTER TABLE tool ADD COLUMN kind TEXT NOT NULL DEFAULT 'cli';
ALTER TABLE tool ADD COLUMN capability TEXT;
ALTER TABLE tool ADD COLUMN scan_target TEXT;
ALTER TABLE tool ADD COLUMN status TEXT NOT NULL DEFAULT 'unknown';
ALTER TABLE tool ADD COLUMN checked_at TEXT;

UPDATE tool SET kind = 'mcp' WHERE command LIKE 'mcp:%';
UPDATE tool SET kind = 'skill' WHERE command LIKE 'skill:%';

INSERT INTO schema_version (version) VALUES (5);
