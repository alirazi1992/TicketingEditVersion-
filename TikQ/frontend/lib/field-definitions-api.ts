import { apiRequest } from "./api-client";

export interface FieldOption {
  value: string;
  label: string;
}

export interface FieldDefinitionResponse {
  id: number;
  categoryId?: number | null;
  subcategoryId?: number | null;
  name: string;
  label: string;
  key: string;
  type: string;
  isRequired: boolean;
  defaultValue?: string;
  options?: FieldOption[];
  min?: number;
  max?: number;
  displayOrder?: number;
  isActive?: boolean;
  scopeType?: "Category" | "Subcategory";
}

export interface CreateFieldDefinitionRequest {
  name: string;
  label: string;
  key: string;
  type: string;
  isRequired: boolean;
  defaultValue?: string;
  options?: FieldOption[];
  min?: number;
  max?: number;
  displayOrder?: number;
}

// Admin endpoint (requires admin role)
export async function getFieldDefinitions(
  token: string,
  subcategoryId: number
): Promise<FieldDefinitionResponse[]> {
  try {
    return await apiRequest<FieldDefinitionResponse[]>(
      `/admin/subcategories/${subcategoryId}/fields`,
      {
        method: "GET",
        token,
        silent: false,
      }
    );
  } catch (error: any) {
    // Handle 404 as empty list (no fields defined yet)
    if (error?.status === 404) {
      console.log(`[getFieldDefinitions] 404 for subcategory ${subcategoryId} - returning empty list`);
      return [];
    }
    // Handle 500 errors that might be schema-related (e.g., missing DefaultValue column)
    if (error?.status === 500) {
      const errorBody = error?.body || {};
      const errorMessage = error?.message || "";
      const errorDetail = (errorBody as any)?.message || (errorBody as any)?.error || errorMessage;
      
      if (errorDetail.includes("DefaultValue") || 
          errorDetail.includes("no such column") ||
          errorDetail.includes("schema") ||
          errorDetail.includes("migration") ||
          errorDetail.includes("out of sync")) {
        console.warn(`[getFieldDefinitions] Database schema error for subcategory ${subcategoryId}:`, errorDetail);
        // Throw a user-friendly error that will be shown in the UI
        const friendlyError = new Error("خطای پایگاه داده: لطفاً سرور بک‌اند را راه‌اندازی مجدد کنید تا مایگریشن‌ها اعمال شوند.");
        (friendlyError as any).status = 500;
        (friendlyError as any).body = errorBody;
        throw friendlyError;
      }
    }
    throw error;
  }
}

export async function getCategoryFieldDefinitions(
  token: string,
  categoryId: number
): Promise<FieldDefinitionResponse[]> {
  return await apiRequest<FieldDefinitionResponse[]>(
    `/admin/categories/${categoryId}/fields`,
    {
      method: "GET",
      token,
      silent: false,
    }
  );
}

// Client-safe endpoint (any authenticated user can read active fields)
export async function getClientFieldDefinitions(
  token: string,
  subcategoryId: number
): Promise<FieldDefinitionResponse[]> {
  try {
    return await apiRequest<FieldDefinitionResponse[]>(
      `/subcategories/${subcategoryId}/fields`,
      {
        method: "GET",
        token,
        silent: false,
        // Use no-store to ensure fresh data (admin might have just added fields)
      } as any
    );
  } catch (error: any) {
    // Handle 404 as empty list (no fields defined yet)
    if (error?.status === 404) {
      console.log(`[getClientFieldDefinitions] 404 for subcategory ${subcategoryId} - returning empty list`);
      return [];
    }
    // For other errors, log and return empty list (don't break the form)
    console.warn(`[getClientFieldDefinitions] Error for subcategory ${subcategoryId}:`, error);
    return [];
  }
}

export async function createFieldDefinition(
  token: string,
  subcategoryId: number,
  request: CreateFieldDefinitionRequest
): Promise<FieldDefinitionResponse> {
  return await apiRequest<FieldDefinitionResponse>(
    `/admin/subcategories/${subcategoryId}/fields`,
    {
      method: "POST",
      token,
      body: request,
      silent: false,
    }
  );
}

export async function createCategoryFieldDefinition(
  token: string,
  categoryId: number,
  request: CreateFieldDefinitionRequest
): Promise<FieldDefinitionResponse> {
  return await apiRequest<FieldDefinitionResponse>(
    `/admin/categories/${categoryId}/fields`,
    {
      method: "POST",
      token,
      body: request,
      silent: false,
    }
  );
}

export async function updateFieldDefinition(
  token: string,
  subcategoryId: number,
  fieldId: number,
  request: Partial<CreateFieldDefinitionRequest>
): Promise<FieldDefinitionResponse> {
  return await apiRequest<FieldDefinitionResponse>(
    `/admin/subcategories/${subcategoryId}/fields/${fieldId}`,
    {
      method: "PUT",
      token,
      body: request,
      silent: false,
    }
  );
}

export async function updateCategoryFieldDefinition(
  token: string,
  categoryId: number,
  fieldId: number,
  request: Partial<CreateFieldDefinitionRequest>
): Promise<FieldDefinitionResponse> {
  return await apiRequest<FieldDefinitionResponse>(
    `/admin/categories/${categoryId}/fields/${fieldId}`,
    {
      method: "PUT",
      token,
      body: request,
      silent: false,
    }
  );
}

export async function deleteFieldDefinition(
  token: string,
  subcategoryId: number,
  fieldId: number
): Promise<void> {
  return await apiRequest<void>(
    `/admin/subcategories/${subcategoryId}/fields/${fieldId}`,
    {
      method: "DELETE",
      token,
      silent: false,
    }
  );
}

export async function deleteCategoryFieldDefinition(
  token: string,
  categoryId: number,
  fieldId: number
): Promise<void> {
  return await apiRequest<void>(
    `/admin/categories/${categoryId}/fields/${fieldId}`,
    {
      method: "DELETE",
      token,
      silent: false,
    }
  );
}

export async function getEffectiveFieldDefinitions(
  token: string,
  categoryId: number,
  subcategoryId?: number | null
): Promise<FieldDefinitionResponse[]> {
  const params = new URLSearchParams({ categoryId: String(categoryId) })
  if (subcategoryId) {
    params.set("subcategoryId", String(subcategoryId))
  }
  return await apiRequest<FieldDefinitionResponse[]>(
    `/tickets/field-definitions?${params.toString()}`,
    {
      method: "GET",
      token,
      silent: false,
    }
  )
}
