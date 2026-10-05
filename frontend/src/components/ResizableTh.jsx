/** <th> con handle de arrastre para redimensionar columnas (usar dentro de una tabla con table-layout: fixed + colgroup). */
export default function ResizableTh({ children, onResizeStart, className = '', ...rest }) {
  return (
    <th className={`dss-resizable-th ${className}`} {...rest}>
      <span className="dss-th-label">{children}</span>
      <span
        className="dss-col-resize-handle"
        onMouseDown={onResizeStart}
        onTouchStart={onResizeStart}
        onClick={(e) => e.stopPropagation()}
      />
    </th>
  )
}
