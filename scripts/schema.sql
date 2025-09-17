--
-- PostgreSQL database dump
--

-- Dumped from database version 17.5 (Debian 17.5-1.pgdg120+1)
-- Dumped by pg_dump version 17.5 (Debian 17.5-1.pgdg120+1)

SET statement_timeout = 0;
SET lock_timeout = 0;
SET idle_in_transaction_session_timeout = 0;
SET transaction_timeout = 0;
SET client_encoding = 'UTF8';
SET standard_conforming_strings = on;
SELECT pg_catalog.set_config('search_path', '', false);
SET check_function_bodies = false;
SET xmloption = content;
SET client_min_messages = warning;
SET row_security = off;

--
-- Name: public; Type: SCHEMA; Schema: -; Owner: pg_database_owner
--

CREATE SCHEMA public;


ALTER SCHEMA public OWNER TO pg_database_owner;

--
-- Name: SCHEMA public; Type: COMMENT; Schema: -; Owner: pg_database_owner
--

COMMENT ON SCHEMA public IS 'standard public schema';


--
-- Name: fn_mm_clear_old_by_batch(bigint, bigint); Type: FUNCTION; Schema: public; Owner: admin
--

CREATE FUNCTION public.fn_mm_clear_old_by_batch(p_dataset_id bigint, p_batch_id bigint) RETURNS void
    LANGUAGE plpgsql
    AS $$
BEGIN
  -- Step 0：刪除指定資料集與批次的舊結果，避免重複累計
  DELETE FROM materialized_metrics_by_batch
  WHERE dataset_id = p_dataset_id
    AND batch_id   = p_batch_id;
END;
$$;


ALTER FUNCTION public.fn_mm_clear_old_by_batch(p_dataset_id bigint, p_batch_id bigint) OWNER TO admin;

--
-- Name: fn_mm_insert_metrics_by_batch(bigint, bigint); Type: FUNCTION; Schema: public; Owner: admin
--

CREATE FUNCTION public.fn_mm_insert_metrics_by_batch(p_dataset_id bigint, p_batch_id bigint) RETURNS void
    LANGUAGE plpgsql
    AS $_$
DECLARE
  v_sentinel_period DATE := DATE '1900-01-01';
BEGIN
  -- 讓同一批可重跑：先清掉舊的 by-batch 結果
  DELETE FROM materialized_metrics_by_batch
  WHERE dataset_id = p_dataset_id AND batch_id = p_batch_id;

  /* SystemField 對應（請確認與 C# Enum SystemField 一致）
     Name=0, Email=1, Phone=2, Gender=3, BirthDate=4, Age=5,
     CustomerId=6, OrderId=7, OrderDate=8, OrderAmount=9,
     OrderStatus=10, Region=11, ProductCategory=12
  */

  -- Step 1：欄位對應表（若同一 system_field 有多筆，DISTINCT ON 取一筆）
  CREATE TEMP TABLE _map ON COMMIT DROP AS
  SELECT DISTINCT ON (system_field)
         system_field::INT AS system_field,
         source_column     AS column_name
  FROM dataset_mappings
  WHERE batch_id = p_batch_id
  ORDER BY system_field, source_column;

  -- Step 1.5：檢查是否有 customerid mapping
  CREATE TEMP TABLE _has_customer_mapping ON COMMIT DROP AS
  SELECT EXISTS(
    SELECT 1 FROM _map WHERE system_field = 6
  ) AS has_customer_mapping;

  -- Step 2：依 mapping 取原始文字值
  CREATE TEMP TABLE _rows_raw ON COMMIT DROP AS
  SELECT
    p_dataset_id AS dataset_id,
    p_batch_id   AS batch_id,
    (r.row_json ->> (SELECT column_name FROM _map WHERE system_field = 8  LIMIT 1)) AS order_date_txt,
    (r.row_json ->> (SELECT column_name FROM _map WHERE system_field = 9  LIMIT 1)) AS order_amount_txt,
    (r.row_json ->> (SELECT column_name FROM _map WHERE system_field = 3  LIMIT 1)) AS gender_txt,
    (r.row_json ->> (SELECT column_name FROM _map WHERE system_field = 5  LIMIT 1)) AS age_txt,
    (r.row_json ->> (SELECT column_name FROM _map WHERE system_field = 11 LIMIT 1)) AS region_txt,
    (r.row_json ->> (SELECT column_name FROM _map WHERE system_field = 12 LIMIT 1)) AS product_category_txt,
    (r.row_json ->> (SELECT column_name FROM _map WHERE system_field = 6  LIMIT 1)) AS customer_id_txt,
    (r.row_json ->> (SELECT column_name FROM _map WHERE system_field = 7  LIMIT 1)) AS order_id_txt,
    (r.row_json ->> (SELECT column_name FROM _map WHERE system_field = 10 LIMIT 1)) AS order_status_txt
  FROM dataset_rows r
  JOIN dataset_batches b ON b.id = r.batch_id
  WHERE r.batch_id   = p_batch_id
    AND b.dataset_id = p_dataset_id;

  -- Step 3：正規化 + 衍生欄位（period 缺值落到 sentinel 月）
  CREATE TEMP TABLE _norm ON COMMIT DROP AS
  WITH t AS (
    SELECT
      dataset_id, batch_id,
          -- 使用安全的日期轉換函數
      safe_date_convert(order_date_txt) AS order_date,
      CASE WHEN order_amount_txt ~ '^\s*-?\d+(\.\d+)?\s*$' THEN TRIM(order_amount_txt)::NUMERIC END AS order_amount,
      NULLIF(LOWER(COALESCE(gender_txt, '')), '')            AS gender,
      -- 限制年齡範圍在 0-100 歲
      CASE 
        WHEN age_txt ~ '^[0-9]+$' AND age_txt::INT BETWEEN 0 AND 100
        THEN TRIM(age_txt)::INT 
        ELSE NULL 
      END AS age,
      NULLIF(COALESCE(region_txt, ''), '')                   AS region,
      NULLIF(COALESCE(product_category_txt, ''), '')         AS product_category,
      NULLIF(COALESCE(customer_id_txt, ''), '')              AS customer_id,
      NULLIF(COALESCE(order_id_txt, ''), '')                 AS order_id,
      NULLIF(LOWER(COALESCE(order_status_txt, '')), '')      AS order_status
    FROM _rows_raw
  )
  SELECT
    dataset_id,
    batch_id,
    order_date,
    order_amount,
    gender,
    age,
    -- 進一步清理地區名稱
  CASE 
    WHEN region IS NULL THEN NULL
    WHEN region ~ '^[?]+$' THEN NULL  -- 過濾純問號
    WHEN region ~ '^[^a-zA-Z\u4e00-\u9fff]+$' THEN NULL  -- 過濾非字母和非中文字元
    ELSE region
  END AS region,
    product_category,
    customer_id,
    order_id,
    order_status,
    COALESCE(date_trunc('month', order_date)::DATE, v_sentinel_period) AS period_month,
    CASE
      WHEN age IS NULL THEN NULL
      ELSE CONCAT((age/10)*10, '-', (age/10)*10 + 9)
    END AS age_bucket
  FROM t;

  -- Step 4：寫入 materialized_metrics_by_batch

  -- 0 TotalRevenue：每月營收 (Sum)
  INSERT INTO materialized_metrics_by_batch (dataset_id, batch_id, metric_key, bucket, period, sum_value)
  SELECT dataset_id, batch_id, 0, NULL::TEXT, period_month, SUM(order_amount)
  FROM _norm
  WHERE order_amount IS NOT NULL
  GROUP BY dataset_id, batch_id, period_month;

  -- 2 TotalOrders：每月訂單數 (Count)
  INSERT INTO materialized_metrics_by_batch (dataset_id, batch_id, metric_key, bucket, period, count_value)
  SELECT dataset_id, batch_id, 2, NULL, period_month, COUNT(DISTINCT order_id)
  FROM _norm
  WHERE order_id IS NOT NULL 
  GROUP BY dataset_id, batch_id, period_month;

  -- 3 AvgOrderValue：平均訂單金額（分母以有金額的列）
  INSERT INTO materialized_metrics_by_batch (dataset_id, batch_id, metric_key, bucket, period, sum_value, count_value)
  SELECT dataset_id, batch_id, 3, NULL, period_month, SUM(order_amount), COUNT(order_amount)
  FROM _norm
  WHERE order_amount IS NOT NULL
  GROUP BY dataset_id, batch_id, period_month;

  -- 12 GenderShare：性別占比
  INSERT INTO materialized_metrics_by_batch (dataset_id, batch_id, metric_key, bucket, period, count_value)
  SELECT 
  	dataset_id,
	batch_id,
	12,
	gender,
	v_sentinel_period,
	CASE 
      WHEN (SELECT has_customer_mapping FROM _has_customer_mapping) THEN 
        -- 有 customerid mapping：計算每個性別的客戶數（去重）
        COUNT(DISTINCT customer_id)
      ELSE 
        -- 沒有 customerid mapping：計算每個性別的記錄數
        COUNT(*)
    END
  FROM _norm
  WHERE gender IS NOT NULL AND gender <> ''
  GROUP BY dataset_id, batch_id, gender;

  -- 11 AgeDistribution：年齡分布 (不按月份分組)
  INSERT INTO materialized_metrics_by_batch (dataset_id, batch_id, metric_key, bucket, period, count_value)
  SELECT 
  	dataset_id,
	batch_id,
	11,
	age_bucket,
	v_sentinel_period,
	CASE 
      WHEN (SELECT has_customer_mapping FROM _has_customer_mapping) THEN 
        -- 有 customerid mapping：計算每個年齡段的客戶數（去重）
        COUNT(DISTINCT customer_id)
      ELSE 
        -- 沒有 customerid mapping：計算每個年齡段的記錄數
        COUNT(*)
    END
  FROM _norm
  WHERE age_bucket IS NOT NULL
  GROUP BY dataset_id, batch_id, age_bucket;

  -- 10 RegionDistribution：地區占比 (不按月份分組)
  INSERT INTO materialized_metrics_by_batch (dataset_id, batch_id, metric_key, bucket, period, count_value)
  SELECT 
  	dataset_id,
	batch_id,
	10,
	region,
	v_sentinel_period,
	CASE 
      WHEN (SELECT has_customer_mapping FROM _has_customer_mapping) THEN 
        -- 有 customerid mapping：計算每個地區的客戶數（去重）
        COUNT(DISTINCT customer_id)
      ELSE 
        -- 沒有 customerid mapping：計算每個地區的記錄數
        COUNT(*)
    END
  FROM _norm
  WHERE region IS NOT NULL AND region <> ''
  GROUP BY dataset_id, batch_id, region;

  -- 8 ProductCategorySales：商品分類銷量
  INSERT INTO materialized_metrics_by_batch (dataset_id, batch_id, metric_key, bucket, period, count_value)
  SELECT dataset_id, batch_id, 8, product_category, period_month, COUNT(*)
  FROM _norm
  WHERE product_category IS NOT NULL AND product_category <> ''
  GROUP BY dataset_id, batch_id, product_category, period_month;

  -- 9 MonthlyRevenueTrend：每月營收趨勢
  INSERT INTO materialized_metrics_by_batch (dataset_id, batch_id, metric_key, bucket, period, sum_value)
  SELECT dataset_id, batch_id, 9, NULL, period_month, SUM(order_amount)
  FROM _norm
  WHERE order_amount IS NOT NULL
  GROUP BY dataset_id, batch_id, period_month;

  -- 6 PendingOrders：待處理訂單數
  INSERT INTO materialized_metrics_by_batch (dataset_id, batch_id, metric_key, bucket, period, count_value)
  SELECT dataset_id, batch_id, 6, NULL, period_month, COUNT(*)
  FROM _norm
  WHERE order_status = 'pending'
  GROUP BY dataset_id, batch_id, period_month;

  -- 1 TotalCustomers：客戶數（根據是否有 customerid mapping 決定計算方式）
  INSERT INTO materialized_metrics_by_batch (dataset_id, batch_id, metric_key, bucket, period, count_value)
  SELECT 
    dataset_id,
	batch_id,
	1, 
	NULL,
	period_month, 
	CASE 
      WHEN (SELECT has_customer_mapping FROM _has_customer_mapping) THEN 
        -- 有 customerid mapping：計算去重後的客戶數
        COUNT(DISTINCT customer_id)
      ELSE 
        -- 沒有 customerid mapping：計算總筆數
        COUNT(*)
    END
  FROM _norm
  WHERE customer_id IS NOT NULL AND customer_id <> ''
  GROUP BY dataset_id, batch_id, period_month;

END;
$_$;


ALTER FUNCTION public.fn_mm_insert_metrics_by_batch(p_dataset_id bigint, p_batch_id bigint) OWNER TO admin;

--
-- Name: fn_mm_upsert_final_metrics(bigint, bigint); Type: FUNCTION; Schema: public; Owner: admin
--

CREATE FUNCTION public.fn_mm_upsert_final_metrics(p_dataset_id bigint, p_batch_id bigint) RETURNS void
    LANGUAGE plpgsql
    AS $$
BEGIN
  -- 直接重新計算該資料集的所有指標
  -- 1) 刪除所有現有數據
  DELETE FROM materialized_metrics 
  WHERE dataset_id = p_dataset_id;

  -- 2) 重新計算所有指標
  CREATE TEMP TABLE _all_metrics ON COMMIT DROP AS
  SELECT 
    dataset_id,
    metric_key,
    bucket,
    period,
    CASE 
      WHEN metric_key IN (0, 9) THEN SUM(sum_value)::NUMERIC(20,4)
      WHEN metric_key IN (1, 2, 6, 8,11) THEN SUM(count_value)::NUMERIC(20,4)
      WHEN metric_key = 3 THEN (SUM(sum_value) / NULLIF(SUM(count_value), 0))::NUMERIC(20,4)
      WHEN metric_key IN ( 10,12) THEN 
        -- 修正：不按 period 分組，計算整個資料集的分布
        (SUM(count_value)::NUMERIC / NULLIF(SUM(SUM(count_value)) OVER (PARTITION BY dataset_id, metric_key), 0))::NUMERIC(20,4)
    END AS value
  FROM materialized_metrics_by_batch
  WHERE dataset_id = p_dataset_id
  GROUP BY dataset_id, metric_key, bucket, period;

  -- 3) 插入新數據
  INSERT INTO materialized_metrics (dataset_id, metric_key, bucket, period, value, updated_at)
  SELECT dataset_id, metric_key, bucket, period, value, NOW()
  FROM _all_metrics
  WHERE value IS NOT NULL;
END;
$$;


ALTER FUNCTION public.fn_mm_upsert_final_metrics(p_dataset_id bigint, p_batch_id bigint) OWNER TO admin;

--
-- Name: safe_date_convert(text); Type: FUNCTION; Schema: public; Owner: admin
--

CREATE FUNCTION public.safe_date_convert(date_str text) RETURNS date
    LANGUAGE plpgsql
    AS $_$
BEGIN
  -- 檢查是否為空或 null
  IF date_str IS NULL OR TRIM(date_str) = '' THEN
    RETURN NULL;
  END IF;
  
  -- 檢查格式是否為 YYYY-MM-DD
  IF date_str !~ '^\d{4}-\d{2}-\d{2}$' THEN
    RETURN NULL;
  END IF;
  
  -- 嘗試轉換並檢查範圍
  BEGIN
    DECLARE
      result_date DATE;
    BEGIN
      result_date := date_str::DATE;
      
      -- 檢查日期範圍
      IF result_date >= '1900-01-01'::DATE AND result_date <= '2100-12-31'::DATE THEN
        RETURN result_date;
      ELSE
        RETURN NULL;
      END IF;
    EXCEPTION WHEN OTHERS THEN
      RETURN NULL;
    END;
  END;
END;
$_$;


ALTER FUNCTION public.safe_date_convert(date_str text) OWNER TO admin;

--
-- Name: set_updated_at(); Type: FUNCTION; Schema: public; Owner: admin
--

CREATE FUNCTION public.set_updated_at() RETURNS trigger
    LANGUAGE plpgsql
    AS $$
    BEGIN
      NEW.updated_at := NOW();
      RETURN NEW;
    END;
    $$;


ALTER FUNCTION public.set_updated_at() OWNER TO admin;

--
-- Name: sp_mm_by_batch(bigint, bigint); Type: PROCEDURE; Schema: public; Owner: admin
--

CREATE PROCEDURE public.sp_mm_by_batch(IN p_dataset_id bigint, IN p_batch_id bigint)
    LANGUAGE plpgsql
    AS $_$
BEGIN
  -- Step 0：刪除指定資料集與批次的舊結果，避免重複累計
  DELETE FROM materialized_metrics_by_batch
  WHERE dataset_id = p_dataset_id
    AND batch_id   = p_batch_id;

  /* SystemField enum 對應（請確認與 C# Enum SystemField 一致）
     Name=0, Email=1, Phone=2, Gender=3, BirthDate=4, Age=5,
     CustomerId=6, OrderId=7, OrderDate=8, OrderAmount=9,
     OrderStatus=10, Region=11, ProductCategory=12
  */

  -- Step 1：建立欄位對應表，記錄每個 system_field 對應的來源欄位名稱
  CREATE TEMP TABLE _map ON COMMIT DROP AS
  SELECT system_field::INT AS system_field,
         source_column     AS column_name
  FROM dataset_mappings
  WHERE batch_id = p_batch_id;

  -- Step 2：根據 mapping 從 dataset_rows 讀取原始文字欄位
  --         透過 JOIN dataset_batches 驗證該批次屬於指定的 dataset
  CREATE TEMP TABLE _rows_raw ON COMMIT DROP AS
  SELECT
    p_dataset_id AS dataset_id,  -- 資料集編號（常數）
    p_batch_id   AS batch_id,    -- 批次編號（常數）
    (r.row_json ->> (SELECT column_name FROM _map WHERE system_field = 8  LIMIT 1)) AS order_date_txt,
    (r.row_json ->> (SELECT column_name FROM _map WHERE system_field = 9  LIMIT 1)) AS order_amount_txt,
    (r.row_json ->> (SELECT column_name FROM _map WHERE system_field = 3  LIMIT 1)) AS gender_txt,
    (r.row_json ->> (SELECT column_name FROM _map WHERE system_field = 5  LIMIT 1)) AS age_txt,
    (r.row_json ->> (SELECT column_name FROM _map WHERE system_field = 11 LIMIT 1)) AS region_txt,
    (r.row_json ->> (SELECT column_name FROM _map WHERE system_field = 12 LIMIT 1)) AS product_category_txt,
    (r.row_json ->> (SELECT column_name FROM _map WHERE system_field = 6  LIMIT 1)) AS customer_id_txt,
    (r.row_json ->> (SELECT column_name FROM _map WHERE system_field = 7  LIMIT 1)) AS order_id_txt,
    (r.row_json ->> (SELECT column_name FROM _map WHERE system_field = 10 LIMIT 1)) AS order_status_txt
  FROM dataset_rows r
  JOIN dataset_batches b ON b.id = r.batch_id
  WHERE r.batch_id   = p_batch_id
    AND b.dataset_id = p_dataset_id;

  -- Step 3：將文字欄位正規化為對應型別，並計算衍生欄位（月份、年齡區間）
  CREATE TEMP TABLE _norm ON COMMIT DROP AS
  SELECT
    dataset_id,
    batch_id,
    -- 訂單日期：僅接受 YYYY-MM-DD 格式
    CASE WHEN order_date_txt   ~ '^\d{4}-\d{2}-\d{2}$'         THEN order_date_txt::DATE END            AS order_date,
    -- 訂單金額：去除空白後轉為數值
    CASE WHEN order_amount_txt ~ '^\s*-?\d+(\.\d+)?\s*$'      THEN TRIM(order_amount_txt)::NUMERIC END AS order_amount,
    -- 性別：轉為小寫並移除空字串
    NULLIF(LOWER(COALESCE(gender_txt, '')), '')                   AS gender,
    -- 年齡：僅接受整數
    CASE WHEN age_txt          ~ '^\s*\d+\s*$'                  THEN TRIM(age_txt)::INT END             AS age,
    -- 地區：移除空字串
    NULLIF(COALESCE(region_txt, ''), '')                         AS region,
    -- 商品分類：移除空字串
    NULLIF(COALESCE(product_category_txt, ''), '')               AS product_category,
    -- 客戶編號：移除空字串
    NULLIF(COALESCE(customer_id_txt, ''), '')                    AS customer_id,
    -- 訂單編號：移除空字串
    NULLIF(COALESCE(order_id_txt, ''), '')                       AS order_id,
    -- 訂單狀態：轉為小寫並移除空字串
    NULLIF(LOWER(COALESCE(order_status_txt, '')), '')            AS order_status,
    -- 期間（月）：將訂單日期截斷到月
    DATE_TRUNC('month',
      CASE WHEN order_date_txt ~ '^\d{4}-\d{2}-\d{2}$' THEN order_date_txt::DATE END
    )::DATE AS period_month,
    -- 年齡區間：以十歲為一區間，例如 20-29
    CASE
      WHEN (CASE WHEN age_txt ~ '^\s*\d+\s*$' THEN TRIM(age_txt)::INT END) IS NULL THEN NULL
      ELSE CONCAT(((TRIM(age_txt)::INT)/10)*10, '-', ((TRIM(age_txt)::INT)/10)*10 + 9)
    END AS age_bucket
  FROM _rows_raw;

  -- Step 4：依指標類型寫入 materialized_metrics_by_batch

  -- 0 TotalRevenue：每月營收 (Sum)
  INSERT INTO materialized_metrics_by_batch (dataset_id, batch_id, metric_key, bucket, period, sum_value)
  SELECT dataset_id, batch_id, 0, NULL::TEXT, period_month, SUM(order_amount)
  FROM _norm
  WHERE order_amount IS NOT NULL AND period_month IS NOT NULL
  GROUP BY dataset_id, batch_id, period_month;

  -- 2 TotalOrders：每月訂單數 (Count)
  INSERT INTO materialized_metrics_by_batch (dataset_id, batch_id, metric_key, bucket, period, count_value)
  SELECT dataset_id, batch_id, 2, NULL, period_month, COUNT(*)
  FROM _norm
  WHERE period_month IS NOT NULL
  GROUP BY dataset_id, batch_id, period_month;

  -- 3 AvgOrderValue：平均訂單金額 (先存 sum + count，後續計算平均)
  INSERT INTO materialized_metrics_by_batch (dataset_id, batch_id, metric_key, bucket, period, sum_value, count_value)
  SELECT dataset_id, batch_id, 3, NULL, period_month, SUM(order_amount), COUNT(order_id)
  FROM _norm
  WHERE order_amount IS NOT NULL AND period_month IS NOT NULL
  GROUP BY dataset_id, batch_id, period_month;

  -- 12 GenderShare：性別占比 (各性別筆數)
  INSERT INTO materialized_metrics_by_batch (dataset_id, batch_id, metric_key, bucket, period, count_value)
  SELECT dataset_id, batch_id, 12, gender, period_month, COUNT(*)
  FROM _norm
  WHERE gender IS NOT NULL AND gender <> '' AND period_month IS NOT NULL
  GROUP BY dataset_id, batch_id, gender, period_month;

  -- 11 AgeDistribution：年齡分布 (各年齡區間筆數)
  INSERT INTO materialized_metrics_by_batch (dataset_id, batch_id, metric_key, bucket, period, count_value)
  SELECT dataset_id, batch_id, 11, age_bucket, period_month, COUNT(*)
  FROM _norm
  WHERE age_bucket IS NOT NULL AND period_month IS NOT NULL
  GROUP BY dataset_id, batch_id, age_bucket, period_month;

  -- 10 RegionDistribution：地區占比 (各地區筆數)
  INSERT INTO materialized_metrics_by_batch (dataset_id, batch_id, metric_key, bucket, period, count_value)
  SELECT dataset_id, batch_id, 10, region, period_month, COUNT(*)
  FROM _norm
  WHERE region IS NOT NULL AND region <> '' AND period_month IS NOT NULL
  GROUP BY dataset_id, batch_id, region, period_month;

  -- 8 ProductCategorySales：商品分類營收 (分類加總)
  INSERT INTO materialized_metrics_by_batch (dataset_id, batch_id, metric_key, bucket, period, sum_value)
  SELECT dataset_id, batch_id, 8, product_category, period_month, SUM(order_amount)
  FROM _norm
  WHERE product_category IS NOT NULL AND product_category <> ''
    AND order_amount IS NOT NULL AND period_month IS NOT NULL
  GROUP BY dataset_id, batch_id, product_category, period_month;

  -- 9 MonthlyRevenueTrend：每月營收趨勢 (月加總)
  INSERT INTO materialized_metrics_by_batch (dataset_id, batch_id, metric_key, bucket, period, sum_value)
  SELECT dataset_id, batch_id, 9, NULL, period_month, SUM(order_amount)
  FROM _norm
  WHERE order_amount IS NOT NULL AND period_month IS NOT NULL
  GROUP BY dataset_id, batch_id, period_month;

  -- 6 PendingOrders：待處理訂單數 (狀態需為 pending)
  INSERT INTO materialized_metrics_by_batch (dataset_id, batch_id, metric_key, bucket, period, count_value)
  SELECT dataset_id, batch_id, 6, NULL, period_month, COUNT(*)
  FROM _norm
  WHERE order_status = 'pending' AND period_month IS NOT NULL
  GROUP BY dataset_id, batch_id, period_month;

  -- 1 TotalCustomers：客戶數 (去重計數)
  INSERT INTO materialized_metrics_by_batch (dataset_id, batch_id, metric_key, bucket, period, count_value)
  SELECT dataset_id, batch_id, 1, NULL, period_month, COUNT(DISTINCT customer_id)
  FROM _norm
  WHERE customer_id IS NOT NULL AND customer_id <> '' AND period_month IS NOT NULL
  GROUP BY dataset_id, batch_id, period_month;

END;
$_$;


ALTER PROCEDURE public.sp_mm_by_batch(IN p_dataset_id bigint, IN p_batch_id bigint) OWNER TO admin;

--
-- Name: sp_mm_upsert_final_for_affected(bigint, bigint); Type: PROCEDURE; Schema: public; Owner: admin
--

CREATE PROCEDURE public.sp_mm_upsert_final_for_affected(IN p_dataset_id bigint, IN p_batch_id bigint)
    LANGUAGE plpgsql
    AS $$
BEGIN
  -- Step 1：找出本批次受到影響的指標切片
  CREATE TEMP TABLE _affected ON COMMIT DROP AS
  SELECT DISTINCT dataset_id, metric_key, bucket, period
  FROM materialized_metrics_by_batch
  WHERE dataset_id = p_dataset_id
    AND batch_id   = p_batch_id;

  RAISE NOTICE '受影響的切片數量: %', (SELECT COUNT(*) FROM _affected);

  -- Step 2：依指標類型計算合併值

  -- 2-1 Sum 指標 (0, 8, 9)
  CREATE TEMP TABLE _sum_rows ON COMMIT DROP AS
  SELECT b.dataset_id, b.metric_key, b.bucket, b.period,
         SUM(b.sum_value)::NUMERIC(20,4) AS value,
         'sum' AS value_type
  FROM materialized_metrics_by_batch b
  JOIN _affected a
    ON a.dataset_id = b.dataset_id
   AND a.metric_key = b.metric_key
   AND a.period     = b.period
   AND (a.bucket IS NOT DISTINCT FROM b.bucket)
  WHERE b.sum_value IS NOT NULL
  GROUP BY 1,2,3,4
  HAVING SUM(b.sum_value) IS NOT NULL;

  -- 2-2 Count 指標 (1, 2, 6, 11)
  CREATE TEMP TABLE _count_rows ON COMMIT DROP AS
  SELECT b.dataset_id, b.metric_key, b.bucket, b.period,
         SUM(b.count_value)::NUMERIC(20,4) AS value,
         'count' AS value_type
  FROM materialized_metrics_by_batch b
  JOIN _affected a
    ON a.dataset_id = b.dataset_id
   AND a.metric_key = b.metric_key
   AND a.period     = b.period
   AND (a.bucket IS NOT DISTINCT FROM b.bucket)
  WHERE b.count_value IS NOT NULL
  GROUP BY 1,2,3,4
  HAVING SUM(b.count_value) IS NOT NULL;

  -- 2-3 Average 指標 (3) : Σsum / Σcount
  CREATE TEMP TABLE _avg_rows ON COMMIT DROP AS
  SELECT b.dataset_id, b.metric_key, b.bucket, b.period,
         COALESCE(
           CASE
             WHEN SUM(b.count_value) > 0
             THEN (SUM(b.sum_value) / SUM(b.count_value))::NUMERIC(20,4)
             ELSE NULL
           END,
           0::NUMERIC(20,4)
         ) AS value,
         'avg' AS value_type
  FROM materialized_metrics_by_batch b
  JOIN _affected a
    ON a.dataset_id = b.dataset_id
   AND a.metric_key = b.metric_key
   AND a.period     = b.period
   AND (a.bucket IS NOT DISTINCT FROM b.bucket)
  WHERE b.sum_value IS NOT NULL AND b.count_value IS NOT NULL
  GROUP BY 1,2,3,4
  HAVING SUM(b.sum_value) IS NOT NULL AND SUM(b.count_value) > 0;

  -- 2-4 Share 指標 (10, 12) : 各 bucket / 同 period 總數
  CREATE TEMP TABLE _share_counts ON COMMIT DROP AS
  SELECT b.dataset_id, b.metric_key, b.bucket, b.period, SUM(b.count_value) AS cnt
  FROM materialized_metrics_by_batch b
  JOIN _affected a
    ON a.dataset_id = b.dataset_id
   AND a.metric_key = b.metric_key
   AND a.period     = b.period
   AND (a.bucket IS NOT DISTINCT FROM b.bucket)
  WHERE b.count_value IS NOT NULL
  GROUP BY 1,2,3,4
  HAVING SUM(b.count_value) > 0;

  CREATE TEMP TABLE _share_totals ON COMMIT DROP AS
  SELECT dataset_id, metric_key, period, SUM(cnt) AS total_cnt
  FROM _share_counts
  GROUP BY 1,2,3
  HAVING SUM(cnt) > 0;

  CREATE TEMP TABLE _share_rows ON COMMIT DROP AS
  SELECT c.dataset_id, c.metric_key, c.bucket, c.period,
         COALESCE(
           CASE
             WHEN t.total_cnt > 0
             THEN (c.cnt::NUMERIC / t.total_cnt::NUMERIC)::NUMERIC(20,4)
             ELSE NULL
           END,
           0::NUMERIC(20,4)
         ) AS value,
         'share' AS value_type
  FROM _share_counts c
  JOIN _share_totals t
    ON t.dataset_id = c.dataset_id
   AND t.metric_key = c.metric_key
   AND t.period     = c.period
  WHERE t.total_cnt > 0;

  -- Step 3：整合所有類型，得到最終數值
  CREATE TEMP TABLE _final_values ON COMMIT DROP AS
  SELECT DISTINCT
    dataset_id,
    metric_key,
    bucket,
    period,
    CASE
      WHEN metric_key IN (0, 8, 9) THEN (
        SELECT value FROM _sum_rows s
        WHERE s.dataset_id = a.dataset_id
          AND s.metric_key = a.metric_key
          AND (s.bucket IS NOT DISTINCT FROM a.bucket)
          AND s.period = a.period
        LIMIT 1
      )
      WHEN metric_key IN (1, 2, 6, 11) THEN (
        SELECT value FROM _count_rows c
        WHERE c.dataset_id = a.dataset_id
          AND c.metric_key = a.metric_key
          AND (c.bucket IS NOT DISTINCT FROM a.bucket)
          AND c.period = a.period
        LIMIT 1
      )
      WHEN metric_key IN (3) THEN (
        SELECT value FROM _avg_rows av
        WHERE av.dataset_id = a.dataset_id
          AND av.metric_key = a.metric_key
          AND (av.bucket IS NOT DISTINCT FROM a.bucket)
          AND av.period = a.period
        LIMIT 1
      )
      WHEN metric_key IN (10, 12) THEN (
        SELECT value FROM _share_rows sh
        WHERE sh.dataset_id = a.dataset_id
          AND sh.metric_key = a.metric_key
          AND (sh.bucket IS NOT DISTINCT FROM a.bucket)
          AND sh.period = a.period
        LIMIT 1
      )
      ELSE NULL
    END AS value
  FROM _affected a;

  -- Step 4：刪除舊資料並插入新資料
  DELETE FROM materialized_metrics f
  WHERE EXISTS (
    SELECT 1
    FROM _affected a
    WHERE f.dataset_id = a.dataset_id
      AND f.metric_key = a.metric_key
      AND (f.bucket IS NOT DISTINCT FROM a.bucket)
      AND f.period = a.period
  );

  INSERT INTO materialized_metrics
    (dataset_id, metric_key, bucket, period, value, updated_at)
  SELECT dataset_id, metric_key, bucket, period, value, NOW()
  FROM _final_values
  WHERE value IS NOT NULL;

  RAISE NOTICE '插入了 % 筆指標資料', (SELECT COUNT(*) FROM _final_values WHERE value IS NOT NULL);
END;
$$;


ALTER PROCEDURE public.sp_mm_upsert_final_for_affected(IN p_dataset_id bigint, IN p_batch_id bigint) OWNER TO admin;

SET default_tablespace = '';

SET default_table_access_method = heap;

--
-- Name: dataset_batches; Type: TABLE; Schema: public; Owner: admin
--

CREATE TABLE public.dataset_batches (
    id bigint NOT NULL,
    dataset_id bigint NOT NULL,
    source_filename text NOT NULL,
    total_rows bigint DEFAULT 0 NOT NULL,
    status text DEFAULT 'Pending'::text NOT NULL,
    error_message text,
    created_at timestamp with time zone DEFAULT now() NOT NULL,
    updated_at timestamp with time zone DEFAULT now() NOT NULL,
    CONSTRAINT dataset_batches_status_check CHECK ((status = ANY (ARRAY['Pending'::text, 'Mapped'::text, 'Processing'::text, 'Succeeded'::text, 'Failed'::text])))
);


ALTER TABLE public.dataset_batches OWNER TO admin;

--
-- Name: dataset_batches_id_seq; Type: SEQUENCE; Schema: public; Owner: admin
--

ALTER TABLE public.dataset_batches ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.dataset_batches_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: dataset_columns; Type: TABLE; Schema: public; Owner: admin
--

CREATE TABLE public.dataset_columns (
    id bigint NOT NULL,
    batch_id bigint NOT NULL,
    source_name text NOT NULL,
    data_type text,
    sample_value text,
    created_at timestamp with time zone DEFAULT now() NOT NULL,
    updated_at timestamp with time zone DEFAULT now() NOT NULL
);


ALTER TABLE public.dataset_columns OWNER TO admin;

--
-- Name: dataset_columns_id_seq; Type: SEQUENCE; Schema: public; Owner: admin
--

ALTER TABLE public.dataset_columns ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.dataset_columns_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: dataset_mappings; Type: TABLE; Schema: public; Owner: admin
--

CREATE TABLE public.dataset_mappings (
    id bigint NOT NULL,
    batch_id bigint NOT NULL,
    source_column text NOT NULL,
    system_field integer NOT NULL,
    created_at timestamp with time zone DEFAULT now() NOT NULL,
    updated_at timestamp with time zone DEFAULT now() NOT NULL
);


ALTER TABLE public.dataset_mappings OWNER TO admin;

--
-- Name: dataset_mappings_id_seq; Type: SEQUENCE; Schema: public; Owner: admin
--

ALTER TABLE public.dataset_mappings ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.dataset_mappings_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: dataset_rows; Type: TABLE; Schema: public; Owner: admin
--

CREATE TABLE public.dataset_rows (
    id bigint NOT NULL,
    batch_id bigint NOT NULL,
    row_json jsonb NOT NULL,
    created_at timestamp with time zone DEFAULT now() NOT NULL
);


ALTER TABLE public.dataset_rows OWNER TO admin;

--
-- Name: dataset_rows_id_seq; Type: SEQUENCE; Schema: public; Owner: admin
--

ALTER TABLE public.dataset_rows ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.dataset_rows_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: datasets; Type: TABLE; Schema: public; Owner: admin
--

CREATE TABLE public.datasets (
    id bigint NOT NULL,
    name text NOT NULL,
    owner_id bigint,
    created_at timestamp with time zone DEFAULT now() NOT NULL,
    updated_at timestamp with time zone DEFAULT now() NOT NULL
);


ALTER TABLE public.datasets OWNER TO admin;

--
-- Name: datasets_id_seq; Type: SEQUENCE; Schema: public; Owner: admin
--

ALTER TABLE public.datasets ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.datasets_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: materialized_metrics; Type: TABLE; Schema: public; Owner: admin
--

CREATE TABLE public.materialized_metrics (
    id bigint NOT NULL,
    dataset_id bigint NOT NULL,
    metric_key integer NOT NULL,
    bucket text,
    period date,
    value numeric(20,4) NOT NULL,
    updated_at timestamp with time zone DEFAULT now() NOT NULL
);


ALTER TABLE public.materialized_metrics OWNER TO admin;

--
-- Name: materialized_metrics_by_batch; Type: TABLE; Schema: public; Owner: admin
--

CREATE TABLE public.materialized_metrics_by_batch (
    id bigint NOT NULL,
    dataset_id bigint NOT NULL,
    batch_id bigint NOT NULL,
    metric_key integer NOT NULL,
    bucket text,
    period date NOT NULL,
    sum_value numeric(20,4),
    count_value bigint,
    min_value numeric(20,4),
    max_value numeric(20,4),
    numer_value numeric(20,4),
    denom_value numeric(20,4),
    created_at timestamp with time zone DEFAULT now() NOT NULL,
    updated_at timestamp with time zone DEFAULT now() NOT NULL,
    CONSTRAINT materialized_metrics_by_batch_check CHECK (((sum_value IS NOT NULL) OR (count_value IS NOT NULL) OR (min_value IS NOT NULL) OR (max_value IS NOT NULL) OR (numer_value IS NOT NULL) OR (denom_value IS NOT NULL)))
);


ALTER TABLE public.materialized_metrics_by_batch OWNER TO admin;

--
-- Name: materialized_metrics_by_batch_id_seq; Type: SEQUENCE; Schema: public; Owner: admin
--

ALTER TABLE public.materialized_metrics_by_batch ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.materialized_metrics_by_batch_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: materialized_metrics_id_seq; Type: SEQUENCE; Schema: public; Owner: admin
--

ALTER TABLE public.materialized_metrics ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.materialized_metrics_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: users; Type: TABLE; Schema: public; Owner: admin
--

CREATE TABLE public.users (
    id bigint NOT NULL,
    email text NOT NULL,
    display_name text NOT NULL,
    uid text NOT NULL,
    last_login_at timestamp with time zone,
    created_at timestamp with time zone DEFAULT now() NOT NULL,
    updated_at timestamp with time zone DEFAULT now() NOT NULL
);


ALTER TABLE public.users OWNER TO admin;

--
-- Name: users_id_seq; Type: SEQUENCE; Schema: public; Owner: admin
--

ALTER TABLE public.users ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.users_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: dataset_batches dataset_batches_pkey; Type: CONSTRAINT; Schema: public; Owner: admin
--

ALTER TABLE ONLY public.dataset_batches
    ADD CONSTRAINT dataset_batches_pkey PRIMARY KEY (id);


--
-- Name: dataset_columns dataset_columns_batch_id_source_name_key; Type: CONSTRAINT; Schema: public; Owner: admin
--

ALTER TABLE ONLY public.dataset_columns
    ADD CONSTRAINT dataset_columns_batch_id_source_name_key UNIQUE (batch_id, source_name);


--
-- Name: dataset_columns dataset_columns_pkey; Type: CONSTRAINT; Schema: public; Owner: admin
--

ALTER TABLE ONLY public.dataset_columns
    ADD CONSTRAINT dataset_columns_pkey PRIMARY KEY (id);


--
-- Name: dataset_mappings dataset_mappings_batch_id_source_column_key; Type: CONSTRAINT; Schema: public; Owner: admin
--

ALTER TABLE ONLY public.dataset_mappings
    ADD CONSTRAINT dataset_mappings_batch_id_source_column_key UNIQUE (batch_id, source_column);


--
-- Name: dataset_mappings dataset_mappings_batch_id_system_field_key; Type: CONSTRAINT; Schema: public; Owner: admin
--

ALTER TABLE ONLY public.dataset_mappings
    ADD CONSTRAINT dataset_mappings_batch_id_system_field_key UNIQUE (batch_id, system_field);


--
-- Name: dataset_mappings dataset_mappings_pkey; Type: CONSTRAINT; Schema: public; Owner: admin
--

ALTER TABLE ONLY public.dataset_mappings
    ADD CONSTRAINT dataset_mappings_pkey PRIMARY KEY (id);


--
-- Name: dataset_rows dataset_rows_pkey; Type: CONSTRAINT; Schema: public; Owner: admin
--

ALTER TABLE ONLY public.dataset_rows
    ADD CONSTRAINT dataset_rows_pkey PRIMARY KEY (id);


--
-- Name: datasets datasets_pkey; Type: CONSTRAINT; Schema: public; Owner: admin
--

ALTER TABLE ONLY public.datasets
    ADD CONSTRAINT datasets_pkey PRIMARY KEY (id);


--
-- Name: materialized_metrics_by_batch materialized_metrics_by_batch_dataset_id_batch_id_metric_ke_key; Type: CONSTRAINT; Schema: public; Owner: admin
--

ALTER TABLE ONLY public.materialized_metrics_by_batch
    ADD CONSTRAINT materialized_metrics_by_batch_dataset_id_batch_id_metric_ke_key UNIQUE (dataset_id, batch_id, metric_key, bucket, period);


--
-- Name: materialized_metrics_by_batch materialized_metrics_by_batch_pkey; Type: CONSTRAINT; Schema: public; Owner: admin
--

ALTER TABLE ONLY public.materialized_metrics_by_batch
    ADD CONSTRAINT materialized_metrics_by_batch_pkey PRIMARY KEY (id);


--
-- Name: materialized_metrics materialized_metrics_dataset_id_metric_key_bucket_period_key; Type: CONSTRAINT; Schema: public; Owner: admin
--

ALTER TABLE ONLY public.materialized_metrics
    ADD CONSTRAINT materialized_metrics_dataset_id_metric_key_bucket_period_key UNIQUE (dataset_id, metric_key, bucket, period);


--
-- Name: materialized_metrics materialized_metrics_pkey; Type: CONSTRAINT; Schema: public; Owner: admin
--

ALTER TABLE ONLY public.materialized_metrics
    ADD CONSTRAINT materialized_metrics_pkey PRIMARY KEY (id);


--
-- Name: users users_email_key; Type: CONSTRAINT; Schema: public; Owner: admin
--

ALTER TABLE ONLY public.users
    ADD CONSTRAINT users_email_key UNIQUE (email);


--
-- Name: users users_pkey; Type: CONSTRAINT; Schema: public; Owner: admin
--

ALTER TABLE ONLY public.users
    ADD CONSTRAINT users_pkey PRIMARY KEY (id);


--
-- Name: users users_uid_key; Type: CONSTRAINT; Schema: public; Owner: admin
--

ALTER TABLE ONLY public.users
    ADD CONSTRAINT users_uid_key UNIQUE (uid);


--
-- Name: idx_dataset_batches_status; Type: INDEX; Schema: public; Owner: admin
--

CREATE INDEX idx_dataset_batches_status ON public.dataset_batches USING btree (status);


--
-- Name: idx_dataset_columns_batch; Type: INDEX; Schema: public; Owner: admin
--

CREATE INDEX idx_dataset_columns_batch ON public.dataset_columns USING btree (batch_id);


--
-- Name: idx_dataset_mappings_batch; Type: INDEX; Schema: public; Owner: admin
--

CREATE INDEX idx_dataset_mappings_batch ON public.dataset_mappings USING btree (batch_id);


--
-- Name: idx_dataset_rows_batch; Type: INDEX; Schema: public; Owner: admin
--

CREATE INDEX idx_dataset_rows_batch ON public.dataset_rows USING btree (batch_id);


--
-- Name: idx_mm_bucket; Type: INDEX; Schema: public; Owner: admin
--

CREATE INDEX idx_mm_bucket ON public.materialized_metrics USING btree (bucket);


--
-- Name: idx_mm_dataset_metric; Type: INDEX; Schema: public; Owner: admin
--

CREATE INDEX idx_mm_dataset_metric ON public.materialized_metrics USING btree (dataset_id, metric_key);


--
-- Name: idx_mm_period; Type: INDEX; Schema: public; Owner: admin
--

CREATE INDEX idx_mm_period ON public.materialized_metrics USING btree (period);


--
-- Name: idx_mmbb_batch; Type: INDEX; Schema: public; Owner: admin
--

CREATE INDEX idx_mmbb_batch ON public.materialized_metrics_by_batch USING btree (batch_id);


--
-- Name: idx_mmbb_dataset_metric_bucket_period; Type: INDEX; Schema: public; Owner: admin
--

CREATE INDEX idx_mmbb_dataset_metric_bucket_period ON public.materialized_metrics_by_batch USING btree (dataset_id, metric_key, bucket, period);


--
-- Name: idx_mmbb_dataset_metric_period; Type: INDEX; Schema: public; Owner: admin
--

CREATE INDEX idx_mmbb_dataset_metric_period ON public.materialized_metrics_by_batch USING btree (dataset_id, metric_key, period);


--
-- Name: ux_dataset_mappings_batch_field; Type: INDEX; Schema: public; Owner: admin
--

CREATE UNIQUE INDEX ux_dataset_mappings_batch_field ON public.dataset_mappings USING btree (batch_id, system_field);


--
-- Name: dataset_batches trg_dataset_batches_set_updated_at; Type: TRIGGER; Schema: public; Owner: admin
--

CREATE TRIGGER trg_dataset_batches_set_updated_at BEFORE UPDATE ON public.dataset_batches FOR EACH ROW EXECUTE FUNCTION public.set_updated_at();


--
-- Name: dataset_columns trg_dataset_columns_set_updated_at; Type: TRIGGER; Schema: public; Owner: admin
--

CREATE TRIGGER trg_dataset_columns_set_updated_at BEFORE UPDATE ON public.dataset_columns FOR EACH ROW EXECUTE FUNCTION public.set_updated_at();


--
-- Name: dataset_mappings trg_dataset_mappings_set_updated_at; Type: TRIGGER; Schema: public; Owner: admin
--

CREATE TRIGGER trg_dataset_mappings_set_updated_at BEFORE UPDATE ON public.dataset_mappings FOR EACH ROW EXECUTE FUNCTION public.set_updated_at();


--
-- Name: datasets trg_datasets_set_updated_at; Type: TRIGGER; Schema: public; Owner: admin
--

CREATE TRIGGER trg_datasets_set_updated_at BEFORE UPDATE ON public.datasets FOR EACH ROW EXECUTE FUNCTION public.set_updated_at();


--
-- Name: materialized_metrics trg_materialized_metrics_set_updated_at; Type: TRIGGER; Schema: public; Owner: admin
--

CREATE TRIGGER trg_materialized_metrics_set_updated_at BEFORE UPDATE ON public.materialized_metrics FOR EACH ROW EXECUTE FUNCTION public.set_updated_at();


--
-- Name: users trg_users_set_updated_at; Type: TRIGGER; Schema: public; Owner: admin
--

CREATE TRIGGER trg_users_set_updated_at BEFORE UPDATE ON public.users FOR EACH ROW EXECUTE FUNCTION public.set_updated_at();


--
-- Name: dataset_batches dataset_batches_dataset_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: admin
--

ALTER TABLE ONLY public.dataset_batches
    ADD CONSTRAINT dataset_batches_dataset_id_fkey FOREIGN KEY (dataset_id) REFERENCES public.datasets(id) ON DELETE CASCADE;


--
-- Name: dataset_columns dataset_columns_batch_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: admin
--

ALTER TABLE ONLY public.dataset_columns
    ADD CONSTRAINT dataset_columns_batch_id_fkey FOREIGN KEY (batch_id) REFERENCES public.dataset_batches(id) ON DELETE CASCADE;


--
-- Name: dataset_mappings dataset_mappings_batch_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: admin
--

ALTER TABLE ONLY public.dataset_mappings
    ADD CONSTRAINT dataset_mappings_batch_id_fkey FOREIGN KEY (batch_id) REFERENCES public.dataset_batches(id) ON DELETE CASCADE;


--
-- Name: dataset_mappings dataset_mappings_batch_id_source_column_fkey; Type: FK CONSTRAINT; Schema: public; Owner: admin
--

ALTER TABLE ONLY public.dataset_mappings
    ADD CONSTRAINT dataset_mappings_batch_id_source_column_fkey FOREIGN KEY (batch_id, source_column) REFERENCES public.dataset_columns(batch_id, source_name) ON DELETE CASCADE;


--
-- Name: dataset_rows dataset_rows_batch_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: admin
--

ALTER TABLE ONLY public.dataset_rows
    ADD CONSTRAINT dataset_rows_batch_id_fkey FOREIGN KEY (batch_id) REFERENCES public.dataset_batches(id) ON DELETE CASCADE;


--
-- Name: datasets datasets_owner_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: admin
--

ALTER TABLE ONLY public.datasets
    ADD CONSTRAINT datasets_owner_id_fkey FOREIGN KEY (owner_id) REFERENCES public.users(id) ON DELETE SET NULL;


--
-- Name: materialized_metrics_by_batch materialized_metrics_by_batch_batch_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: admin
--

ALTER TABLE ONLY public.materialized_metrics_by_batch
    ADD CONSTRAINT materialized_metrics_by_batch_batch_id_fkey FOREIGN KEY (batch_id) REFERENCES public.dataset_batches(id) ON DELETE CASCADE;


--
-- Name: materialized_metrics materialized_metrics_dataset_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: admin
--

ALTER TABLE ONLY public.materialized_metrics
    ADD CONSTRAINT materialized_metrics_dataset_id_fkey FOREIGN KEY (dataset_id) REFERENCES public.datasets(id) ON DELETE CASCADE;


--
-- PostgreSQL database dump complete
--

