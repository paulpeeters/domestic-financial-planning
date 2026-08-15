SET @column_exists := (
    SELECT COUNT(*)
    FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'app_users'
      AND COLUMN_NAME = 'is_global_admin'
);

SET @ddl := IF(
    @column_exists = 0,
    'ALTER TABLE app_users ADD COLUMN is_global_admin TINYINT(1) NOT NULL DEFAULT 0 AFTER password_hash',
    'SELECT 1'
);
PREPARE stmt FROM @ddl;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- Bootstrap safety: guarantee at least one global admin.
SET @has_global_admin := (SELECT COUNT(*) FROM app_users WHERE is_global_admin = 1);
SET @first_user_id := (SELECT id FROM app_users ORDER BY id ASC LIMIT 1);

UPDATE app_users
SET is_global_admin = 1
WHERE @has_global_admin = 0
  AND id = @first_user_id;
