"use client";

import { useState, useEffect } from "react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { useAuth } from "@/lib/auth-context";
import { setResponsibleTechnician, getTicketTechnicians } from "@/lib/tickets-api";
import type { ApiTicketTechnicianDto } from "@/lib/tickets-api";
import type { ApiTicketResponse } from "@/lib/api-types";
import { toast } from "sonner";

interface TicketResponsibleDelegationProps {
  ticketId: string;
  ticket: ApiTicketResponse;
  onUpdate: () => Promise<void>;
}

export function TicketResponsibleDelegation({ ticketId, ticket, onUpdate }: TicketResponsibleDelegationProps) {
  const { token, user } = useAuth();
  const [assignedTechnicians, setAssignedTechnicians] = useState<ApiTicketTechnicianDto[]>([]);
  const [selectedTechnicianId, setSelectedTechnicianId] = useState<string>("");
  const [loading, setLoading] = useState(false);
  const [loadingList, setLoadingList] = useState(true);

  useEffect(() => {
    const loadTechnicians = async () => {
      if (!user) return;
      try {
        const techs = await getTicketTechnicians(token, ticketId);
        setAssignedTechnicians(techs);
        
        // Set current responsible as selected
        if (ticket.responsibleTechnicianId) {
          setSelectedTechnicianId(ticket.responsibleTechnicianId);
        } else if (techs.length > 0) {
          // Default to first if no responsible set
          setSelectedTechnicianId(techs[0].technicianId);
        }
      } catch (error) {
        console.error("Failed to load technicians:", error);
      } finally {
        setLoadingList(false);
      }
    };
    loadTechnicians();
  }, [token, ticketId, ticket.responsibleTechnicianId]);

  const currentResponsible = assignedTechnicians.find(
    (t) => t.technicianId === ticket.responsibleTechnicianId
  );

  const handleSetResponsible = async () => {
    if (!selectedTechnicianId || selectedTechnicianId === ticket.responsibleTechnicianId) {
      return;
    }

    if (!user) {
      toast.error("دسترسی غیرمجاز");
      return;
    }

    setLoading(true);
    try {
      await setResponsibleTechnician(token, ticketId, {
        technicianId: selectedTechnicianId,
      });

      toast.success("مسئول تیکت با موفقیت تغییر کرد");
      await onUpdate();
    } catch (error: any) {
      console.error("Failed to set responsible technician:", error);
      toast.error(error?.message || "خطا در تغییر مسئول تیکت");
    } finally {
      setLoading(false);
    }
  };

  if (loadingList || assignedTechnicians.length === 0) {
    return null;
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle>انتخاب مسئول تیکت</CardTitle>
      </CardHeader>
      <CardContent className="space-y-4">
        {currentResponsible && (
          <div className="text-sm">
            <span className="text-muted-foreground">مسئول فعلی: </span>
            <span className="font-medium">{currentResponsible.name}</span>
          </div>
        )}
        <div className="flex gap-2">
          <Select value={selectedTechnicianId} onValueChange={setSelectedTechnicianId}>
            <SelectTrigger className="flex-1">
              <SelectValue placeholder="تکنسین را انتخاب کنید" />
            </SelectTrigger>
            <SelectContent>
              {assignedTechnicians.map((tech) => (
                <SelectItem key={tech.technicianId} value={tech.technicianId}>
                  {tech.name}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
          <Button onClick={handleSetResponsible} disabled={loading || !selectedTechnicianId}>
            {loading ? "در حال اعمال..." : "انتخاب مسئول"}
          </Button>
        </div>
      </CardContent>
    </Card>
  );
}

