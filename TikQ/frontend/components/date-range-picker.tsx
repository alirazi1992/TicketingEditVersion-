"use client";

import { Label } from "@/components/ui/label";
import DatePicker from "react-multi-date-picker";
import persian from "react-date-object/calendars/persian";
import persian_fa from "react-date-object/locales/persian_fa";
import { dateToTimestamp, timestampToDate } from "@/lib/date";

export interface DateRangeValue {
  from?: number;
  to?: number;
}

export interface DateRangePickerProps {
  value: DateRangeValue;
  onChange: (next: DateRangeValue) => void;
  error?: string | null;
  labels?: { from?: string; to?: string };
  /** Optional class for the container (e.g. grid gap). */
  className?: string;
  /** Smaller inputs for technician section. */
  size?: "default" | "sm";
}

const defaultLabels = { from: "از تاریخ", to: "تا تاریخ" };

export function DateRangePicker({
  value,
  onChange,
  error,
  labels: propLabels,
  className = "",
  size = "default",
}: DateRangePickerProps) {
  const labels = { ...defaultLabels, ...propLabels };
  const inputClass =
    size === "sm"
      ? "flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-right font-iran text-sm shadow-sm"
      : "flex h-10 w-full rounded-md border border-input bg-transparent px-3 py-2 text-right font-iran text-sm shadow-sm";

  const handleFromChange = (d: unknown) => {
    if (d && typeof d === "object" && "toDate" in d && typeof (d as { toDate: () => Date }).toDate === "function") {
      const date = (d as { toDate: () => Date }).toDate();
      onChange({ ...value, from: dateToTimestamp(date) });
    } else {
      const { to, ...rest } = value;
      onChange({ ...rest, from: undefined });
    }
  };

  const handleToChange = (d: unknown) => {
    if (d && typeof d === "object" && "toDate" in d && typeof (d as { toDate: () => Date }).toDate === "function") {
      const date = (d as { toDate: () => Date }).toDate();
      onChange({ ...value, to: dateToTimestamp(date) });
    } else {
      const { from, ...rest } = value;
      onChange({ ...rest, to: undefined });
    }
  };

  return (
    <div className={className}>
      {error && (
        <p className="text-destructive text-sm font-iran text-right rounded-md bg-destructive/10 p-3 mb-3">
          {error}
        </p>
      )}
      <div className="flex flex-wrap items-end gap-4">
        <div className="space-y-2">
          <Label className="font-iran text-right">{labels.from}</Label>
          <DatePicker
            calendar={persian}
            locale={persian_fa}
            value={value.from != null ? timestampToDate(value.from) : undefined}
            onChange={handleFromChange}
            format="YYYY/MM/DD"
            className="rmdp-rtl text-right font-iran w-40"
            containerClassName="w-40"
            inputClass={inputClass}
          />
        </div>
        <div className="space-y-2">
          <Label className="font-iran text-right">{labels.to}</Label>
          <DatePicker
            calendar={persian}
            locale={persian_fa}
            value={value.to != null ? timestampToDate(value.to) : undefined}
            onChange={handleToChange}
            format="YYYY/MM/DD"
            className="rmdp-rtl text-right font-iran w-40"
            containerClassName="w-40"
            inputClass={inputClass}
          />
        </div>
      </div>
    </div>
  );
}
