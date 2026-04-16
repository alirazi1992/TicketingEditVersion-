/**
 * Automation Coverage API client for admin coverage visualization
 */

import { apiRequest } from "./api-client";

// ============ Types ============

export interface CoverageSummary {
  totalCategories: number;
  totalSubcategories: number;
  totalPairs: number;
  coveredPairsCount: number;
  uncoveredPairsCount: number;
  coveragePercent: number;
  techniciansCount: number;
  ticketsLast30Days: number;
  autoAssignedLast30Days: number;
  unassignedLast30Days: number;
}

export interface CategorySubcategoryPair {
  categoryId: number;
  categoryName: string;
  subcategoryId: number;
  subcategoryName: string;
}

export interface CoveredPair extends CategorySubcategoryPair {
  technicianCount: number;
}

export interface TechnicianCoverage {
  technicianId: string;
  technicianUserId: string;
  technicianName: string;
  isActive: boolean;
  coveredPairsCount: number;
  pairs: CategorySubcategoryPair[];
}

export interface CoverageBreakdown {
  uncoveredPairs: CategorySubcategoryPair[];
  coveredPairs: CoveredPair[];
  technicianCoverage: TechnicianCoverage[];
}

export interface GraphNode {
  id: string;
  type: "category" | "subcategory" | "technician";
  label: string;
  isActive: boolean;
  hasCoverage: boolean;
}

export interface GraphEdge {
  source: string;
  target: string;
  type: "contains" | "covers";
}

export interface CoverageGraph {
  nodes: GraphNode[];
  edges: GraphEdge[];
}

// ============ API Functions ============

/**
 * Get coverage summary KPIs
 */
export async function getCoverageSummary(token: string): Promise<CoverageSummary> {
  return apiRequest<CoverageSummary>(
    "/admin/automation/coverage/summary",
    { method: "GET", token, silent: true }
  );
}

/**
 * Get coverage breakdown (uncovered pairs, covered pairs, technician coverage)
 */
export async function getCoverageBreakdown(token: string): Promise<CoverageBreakdown> {
  return apiRequest<CoverageBreakdown>(
    "/admin/automation/coverage/breakdown",
    { method: "GET", token, silent: true }
  );
}

/**
 * Get coverage graph for visualization
 */
export async function getCoverageGraph(token: string): Promise<CoverageGraph> {
  return apiRequest<CoverageGraph>(
    "/admin/automation/coverage/graph",
    { method: "GET", token, silent: true }
  );
}














