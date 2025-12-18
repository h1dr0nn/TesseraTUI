use std::ffi::{CStr, CString};
use std::os::raw::{c_char, c_double};

/// FFI-safe string buffer for returning results
#[repr(C)]
pub struct FormulaResult {
    pub value: c_double,
    pub error: *mut c_char, // null if success, C string if error
}

impl FormulaResult {
    fn success(value: f64) -> Self {
        FormulaResult {
            value,
            error: std::ptr::null_mut(),
        }
    }

    fn error(msg: &str) -> Self {
        let c_str = CString::new(msg).unwrap();
        FormulaResult {
            value: 0.0,
            error: c_str.into_raw(),
        }
    }
}

/// Free the error string returned by formula functions
/// Call this from C# after reading the error message
#[no_mangle]
pub extern "C" fn tessera_free_string(ptr: *mut c_char) {
    if !ptr.is_null() {
        unsafe {
            let _ = CString::from_raw(ptr);
        }
    }
}

/// Calculate SUM for a column
/// 
/// # Arguments
/// * `column_name` - C string with column name (e.g., "ColumnA")
/// * `values_ptr` - Pointer to array of C strings (null-terminated)
/// * `count` - Number of values in array
/// 
/// # Returns
/// FormulaResult with calculated sum or error message
/// 
/// # Safety
/// Caller must ensure values_ptr points to valid array of count C strings
#[no_mangle]
pub extern "C" fn tessera_sum(
    column_name: *const c_char,
    values_ptr: *const *const c_char,
    count: usize,
) -> FormulaResult {
    if column_name.is_null() || values_ptr.is_null() {
        return FormulaResult::error("Null pointer provided");
    }

    // Parse column name (not used for now, but reserved for future column-based refs)
    let _col_name = unsafe {
        match CStr::from_ptr(column_name).to_str() {
            Ok(s) => s,
            Err(_) => return FormulaResult::error("Invalid column name encoding"),
        }
    };

    // Parse all values and sum them
    let mut sum = 0.0;
    let mut parsed_count = 0;

    unsafe {
        let values = std::slice::from_raw_parts(values_ptr, count);
        for i in 0..count {
            if values[i].is_null() {
                continue; // Skip null values
            }

            let value_str = match CStr::from_ptr(values[i]).to_str() {
                Ok(s) => s.trim(),
                Err(_) => continue, // Skip invalid encoding
            };

            if value_str.is_empty() {
                continue; // Skip empty strings
            }

            // Try parsing as f64 (handles both int and float)
            match value_str.parse::<f64>() {
                Ok(num) => {
                    sum += num;
                    parsed_count += 1;
                }
                Err(_) => {
                    // Skip non-numeric values (could return error, but SUM typically ignores non-numeric)
                    continue;
                }
            }
        }
    }

    if parsed_count == 0 {
        return FormulaResult::error("No numeric values found in column");
    }

    FormulaResult::success(sum)
}

/// Calculate AVG (average) for a column
#[no_mangle]
pub extern "C" fn tessera_avg(
    column_name: *const c_char,
    values_ptr: *const *const c_char,
    count: usize,
) -> FormulaResult {
    if column_name.is_null() || values_ptr.is_null() {
        return FormulaResult::error("Null pointer provided");
    }

    let _col_name = unsafe {
        match CStr::from_ptr(column_name).to_str() {
            Ok(s) => s,
            Err(_) => return FormulaResult::error("Invalid column name encoding"),
        }
    };

    let mut sum = 0.0;
    let mut parsed_count = 0;

    unsafe {
        let values = std::slice::from_raw_parts(values_ptr, count);
        for i in 0..count {
            if values[i].is_null() {
                continue;
            }

            let value_str = match CStr::from_ptr(values[i]).to_str() {
                Ok(s) => s.trim(),
                Err(_) => continue,
            };

            if value_str.is_empty() {
                continue;
            }

            match value_str.parse::<f64>() {
                Ok(num) => {
                    sum += num;
                    parsed_count += 1;
                }
                Err(_) => continue,
            }
        }
    }

    if parsed_count == 0 {
        return FormulaResult::error("No numeric values found in column");
    }

    FormulaResult::success(sum / parsed_count as f64)
}

/// Calculate MIN for a column
#[no_mangle]
pub extern "C" fn tessera_min(
    column_name: *const c_char,
    values_ptr: *const *const c_char,
    count: usize,
) -> FormulaResult {
    if column_name.is_null() || values_ptr.is_null() {
        return FormulaResult::error("Null pointer provided");
    }

    let _col_name = unsafe {
        match CStr::from_ptr(column_name).to_str() {
            Ok(s) => s,
            Err(_) => return FormulaResult::error("Invalid column name encoding"),
        }
    };

    let mut min_value: Option<f64> = None;

    unsafe {
        let values = std::slice::from_raw_parts(values_ptr, count);
        for i in 0..count {
            if values[i].is_null() {
                continue;
            }

            let value_str = match CStr::from_ptr(values[i]).to_str() {
                Ok(s) => s.trim(),
                Err(_) => continue,
            };

            if value_str.is_empty() {
                continue;
            }

            match value_str.parse::<f64>() {
                Ok(num) => {
                    min_value = Some(match min_value {
                        Some(current_min) => current_min.min(num),
                        None => num,
                    });
                }
                Err(_) => continue,
            }
        }
    }

    match min_value {
        Some(min) => FormulaResult::success(min),
        None => FormulaResult::error("No numeric values found in column"),
    }
}

/// Calculate MAX for a column
#[no_mangle]
pub extern "C" fn tessera_max(
    column_name: *const c_char,
    values_ptr: *const *const c_char,
    count: usize,
) -> FormulaResult {
    if column_name.is_null() || values_ptr.is_null() {
        return FormulaResult::error("Null pointer provided");
    }

    let _col_name = unsafe {
        match CStr::from_ptr(column_name).to_str() {
            Ok(s) => s,
            Err(_) => return FormulaResult::error("Invalid column name encoding"),
        }
    };

    let mut max_value: Option<f64> = None;

    unsafe {
        let values = std::slice::from_raw_parts(values_ptr, count);
        for i in 0..count {
            if values[i].is_null() {
                continue;
            }

            let value_str = match CStr::from_ptr(values[i]).to_str() {
                Ok(s) => s.trim(),
                Err(_) => continue,
            };

            if value_str.is_empty() {
                continue;
            }

            match value_str.parse::<f64>() {
                Ok(num) => {
                    max_value = Some(match max_value {
                        Some(current_max) => current_max.max(num),
                        None => num,
                    });
                }
                Err(_) => continue,
            }
        }
    }

    match max_value {
        Some(max) => FormulaResult::success(max),
        None => FormulaResult::error("No numeric values found in column"),
    }
}

/// Calculate COUNT for a column (counts non-null, non-empty values)
#[no_mangle]
pub extern "C" fn tessera_count(
    column_name: *const c_char,
    values_ptr: *const *const c_char,
    count: usize,
) -> FormulaResult {
    if column_name.is_null() || values_ptr.is_null() {
        return FormulaResult::error("Null pointer provided");
    }

    let _col_name = unsafe {
        match CStr::from_ptr(column_name).to_str() {
            Ok(s) => s,
            Err(_) => return FormulaResult::error("Invalid column name encoding"),
        }
    };

    let mut counted = 0;

    unsafe {
        let values = std::slice::from_raw_parts(values_ptr, count);
        for i in 0..count {
            if values[i].is_null() {
                continue;
            }

            let value_str = match CStr::from_ptr(values[i]).to_str() {
                Ok(s) => s.trim(),
                Err(_) => continue,
            };

            if !value_str.is_empty() {
                counted += 1;
            }
        }
    }

    FormulaResult::success(counted as f64)
}

/// Parse a formula string and extract function name and arguments
/// 
/// # Arguments
/// * `formula` - C string with formula (e.g., "=SUM(ColumnA)")
/// 
/// # Returns
/// C string with parsed result or error (caller must free with tessera_free_string)
#[no_mangle]
pub extern "C" fn tessera_parse_formula(formula: *const c_char) -> *mut c_char {
    if formula.is_null() {
        let err = CString::new("Null formula string").unwrap();
        return err.into_raw();
    }

    let formula_str = match unsafe { CStr::from_ptr(formula).to_str() } {
        Ok(s) => s.trim(),
        Err(_) => {
            let err = CString::new("Invalid formula encoding").unwrap();
            return err.into_raw();
        }
    };

    if !formula_str.starts_with('=') {
        let err = CString::new("Formula must start with '='").unwrap();
        return err.into_raw();
    }

    // Simple parser for "=SUM(ColumnName)" format
    let formula_body = &formula_str[1..].trim();
    
    if let Some(func_end) = formula_body.find('(') {
        let func_name = &formula_body[..func_end].trim().to_uppercase();
        let args_start = func_end + 1;
        
        if !formula_body.ends_with(')') {
            let err = CString::new("Formula missing closing parenthesis").unwrap();
            return err.into_raw();
        }

        let args = &formula_body[args_start..formula_body.len() - 1].trim();

        // Return parsed structure as JSON-like string for now
        // Format: "FUNCTION:ColumnName"
        let result = format!("{}:{}", func_name, args);
        
        match CString::new(result) {
            Ok(c_str) => c_str.into_raw(),
            Err(_) => {
                let err = CString::new("Failed to create result string").unwrap();
                err.into_raw()
            }
        }
    } else {
        let err = CString::new("Invalid formula syntax: expected function(arg)").unwrap();
        err.into_raw()
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::ffi::CString;

    #[test]
    fn test_parse_formula() {
        let formula = CString::new("=SUM(ColumnA)").unwrap();
        let result_ptr = tessera_parse_formula(formula.as_ptr());
        let result = unsafe { CStr::from_ptr(result_ptr).to_str().unwrap() };
        assert_eq!(result, "SUM:ColumnA");
        tessera_free_string(result_ptr);
    }

    #[test]
    fn test_sum_basic() {
        let col_name = CString::new("Test").unwrap();
        let values = vec![
            CString::new("10").unwrap(),
            CString::new("20").unwrap(),
            CString::new("30").unwrap(),
        ];
        let ptrs: Vec<*const c_char> = values.iter().map(|v| v.as_ptr()).collect();
        
        let result = tessera_sum(col_name.as_ptr(), ptrs.as_ptr(), ptrs.len());
        assert_eq!(result.value, 60.0);
        assert!(result.error.is_null());
    }

    #[test]
    fn test_avg_basic() {
        let col_name = CString::new("Test").unwrap();
        let values = vec![
            CString::new("10").unwrap(),
            CString::new("20").unwrap(),
            CString::new("30").unwrap(),
        ];
        let ptrs: Vec<*const c_char> = values.iter().map(|v| v.as_ptr()).collect();
        
        let result = tessera_avg(col_name.as_ptr(), ptrs.as_ptr(), ptrs.len());
        assert_eq!(result.value, 20.0);
        assert!(result.error.is_null());
    }

    #[test]
    fn test_min_basic() {
        let col_name = CString::new("Test").unwrap();
        let values = vec![
            CString::new("10").unwrap(),
            CString::new("20").unwrap(),
            CString::new("5").unwrap(),
        ];
        let ptrs: Vec<*const c_char> = values.iter().map(|v| v.as_ptr()).collect();
        
        let result = tessera_min(col_name.as_ptr(), ptrs.as_ptr(), ptrs.len());
        assert_eq!(result.value, 5.0);
        assert!(result.error.is_null());
    }

    #[test]
    fn test_max_basic() {
        let col_name = CString::new("Test").unwrap();
        let values = vec![
            CString::new("10").unwrap(),
            CString::new("20").unwrap(),
            CString::new("5").unwrap(),
        ];
        let ptrs: Vec<*const c_char> = values.iter().map(|v| v.as_ptr()).collect();
        
        let result = tessera_max(col_name.as_ptr(), ptrs.as_ptr(), ptrs.len());
        assert_eq!(result.value, 20.0);
        assert!(result.error.is_null());
    }

    #[test]
    fn test_count_basic() {
        let col_name = CString::new("Test").unwrap();
        let values = vec![
            CString::new("10").unwrap(),
            CString::new("").unwrap(),
            CString::new("30").unwrap(),
            CString::new("40").unwrap(),
        ];
        let ptrs: Vec<*const c_char> = values.iter().map(|v| v.as_ptr()).collect();
        
        let result = tessera_count(col_name.as_ptr(), ptrs.as_ptr(), ptrs.len());
        assert_eq!(result.value, 3.0); // Counts non-empty values
        assert!(result.error.is_null());
    }
}

