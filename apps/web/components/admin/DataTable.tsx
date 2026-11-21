import React from 'react';

interface Column<T> {
  key: keyof T;
  header: string;
  align?: 'left' | 'center' | 'right';
  render?: (value: T[keyof T], row: T) => React.ReactNode;
}

interface Props<T> {
  columns: Column<T>[];
  data: T[];
  emptyMessage?: string;
  /** Width below which rows render as cards. Use 'lg' for wide tables that do not fit a tablet. */
  stackBelow?: 'md' | 'lg';
}

const ALIGN_CLASS: Record<NonNullable<Column<any>['align']>, string> = {
  left: 'text-left',
  center: 'text-center',
  right: 'text-right',
};

// Static class names so Tailwind keeps them.
const STACK = {
  md: { cards: 'md:hidden', table: 'hidden md:block' },
  lg: { cards: 'lg:hidden', table: 'hidden lg:block' },
} as const;

/**
 * A table from `md` (or `lg`) up; below that, one card per row (the first column as the card title, the rest as
 * label/value lines), so phones never need sideways scrolling to reach a row's status or actions.
 */
export function DataTable<T extends { id: string }>({ columns, data, emptyMessage = 'No data.', stackBelow = 'md' }: Props<T>) {
  const cell = (col: Column<T>, row: T) => (col.render ? col.render(row[col.key], row) : String(row[col.key] ?? ''));
  const [title, ...rest] = columns;

  return (
    <>
      <div className={`${STACK[stackBelow].cards} pixel-cut border border-frost/10 bg-panel divide-y divide-frost/10 text-sm`}>
        {data.length === 0 ? (
          <p className="px-4 py-8 text-center text-frost/40">{emptyMessage}</p>
        ) : (
          data.map((row) => (
            <div key={row.id} className="px-4 py-3 space-y-2">
              {title && <div className="font-medium text-frost break-words">{cell(title, row)}</div>}
              {rest.map((col) =>
                col.header ? (
                  <div key={String(col.key)} className="flex items-start justify-between gap-4">
                    <span className="shrink-0 pt-0.5 font-mono text-[10px] uppercase tracking-wider text-frost/50">
                      {col.header}
                    </span>
                    <div className="min-w-0 text-right text-frost/80 break-words">{cell(col, row)}</div>
                  </div>
                ) : (
                  <div key={String(col.key)} className="flex justify-end text-frost/80">{cell(col, row)}</div>
                ),
              )}
            </div>
          ))
        )}
      </div>

      <div className={`${STACK[stackBelow].table} overflow-x-auto pixel-cut border border-frost/10 bg-panel`}>
        <table className="min-w-full divide-y divide-frost/10 text-sm">
          <thead className="bg-void-deep">
            <tr>
              {columns.map((col) => (
                <th
                  key={String(col.key)}
                  className={`px-4 py-3 ${ALIGN_CLASS[col.align ?? 'left']} font-mono text-[10px] font-medium text-frost/60 uppercase tracking-wider`}
                >
                  {col.header}
                </th>
              ))}
            </tr>
          </thead>
          <tbody className="divide-y divide-frost/5">
            {data.length === 0 ? (
              <tr><td colSpan={columns.length} className="px-4 py-8 text-center text-frost/40">{emptyMessage}</td></tr>
            ) : (
              data.map((row) => (
                <tr key={row.id} className="hover:bg-panel-light/60">
                  {columns.map((col) => (
                    <td key={String(col.key)} className={`px-4 py-3 text-frost/80 ${ALIGN_CLASS[col.align ?? 'left']}`}>
                      {cell(col, row)}
                    </td>
                  ))}
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>
    </>
  );
}
