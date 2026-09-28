        org 0
        dc.w 0,1
entry:
        pea -4(a5)
        dc.w $a86e
        dc.w $a8fe
        dc.w $a912
        dc.w $a930
        dc.w $a9cc
        dc.w $a850
        dc.w $a852
        lea state(pc),a4
        ifd COLOR
        clr.l -(sp)
        dc.w $aa2a
        move.l (sp)+,44(a4)
        moveq #32,d5
find_depth:
        clr.w -(sp)
        move.l 44(a4),-(sp)
        move.w d5,-(sp)
        move.l #$00010001,-(sp)
        move.w #$0a14,d0
        dc.w $aaa2
        tst.w (sp)+
        bne set_depth
        lsr.w #1,d5
        bne find_depth
        bra depth_ready
set_depth:
        clr.w -(sp)
        move.l 44(a4),-(sp)
        move.w d5,-(sp)
        move.l #$00010001,-(sp)
        move.w #$0a13,d0
        dc.w $aaa2
        addq.l #2,sp
depth_ready:
        endif
        clr.l -(sp)
        move.w #128,-(sp)
        clr.l -(sp)
        move.l #-1,-(sp)
        ifd COLOR
        dc.w $aa46
        else
        dc.w $a9bd
        endif
        move.l (sp)+,(a4)
        move.l (a4),-(sp)
        dc.w $a873
        clr.w -(sp)
        dc.w $a887
        move.w #12,-(sp)
        dc.w $a88a
        clr.l -(sp)
        pea view_rect(pc)
        pea data_rect(pc)
        ifd ICON_LISTS
        move.l #$00400070,-(sp)
        move.w #19,-(sp)
        else
        ifd SICN_LISTS
        move.l #$001400dc,-(sp)
        move.w #$f060,-(sp)
        else
        move.l #$00100000,-(sp)
        clr.w -(sp)
        endif
        endif
        move.l (a4),-(sp)
        clr.w -(sp)
        clr.w -(sp)
        clr.w -(sp)
        move.w #$100,-(sp)
        move.w #$44,-(sp)
        dc.w $a9e7
        move.l (sp)+,4(a4)
        ifd ICON_LISTS
        bsr fill_icons
        else
        ifd SICN_LISTS
        bsr fill_sicn
        else
        lea labels(pc),a3
        moveq #0,d3
fill:
        moveq #0,d4
        move.b (a3)+,d4
        movem.l d3-d4/a3,-(sp)
        move.l a3,-(sp)
        move.w d4,-(sp)
        clr.w -(sp)
        move.w d3,-(sp)
        move.l 4(a4),-(sp)
        move.w #$58,-(sp)
        dc.w $a9e7
        movem.l (sp)+,d3-d4/a3
        adda.w d4,a3
        addq.w #1,d3
        cmp.w #12,d3
        blt fill
        endif
        endif
        move.w #$100,-(sp)
        move.l 4(a4),-(sp)
        move.w #$2c,-(sp)
        dc.w $a9e7
        move.w #$100,-(sp)
        move.l #$00010000,-(sp)
        move.l 4(a4),-(sp)
        move.w #$5c,-(sp)
        dc.w $a9e7
        bsr draw
        move.w #1,48(a4)
loop:
        clr.w -(sp)
        move.w #$ffff,-(sp)
        pea 8(a4)
        dc.w $a970
        tst.b (sp)+
        beq loop
        move.w 8(a4),d0
        cmp.w #6,d0
        beq update
        cmp.w #8,d0
        beq activate_event
        cmp.w #3,d0
        beq key
        cmp.w #1,d0
        bne loop
        clr.w -(sp)
        move.l 18(a4),-(sp)
        pea 24(a4)
        dc.w $a92c
        cmp.w #3,(sp)+
        bne loop
        move.l (a4),-(sp)
        dc.w $a91f
        move.l (a4),-(sp)
        dc.w $a873
        move.l 18(a4),28(a4)
        pea 28(a4)
        dc.w $a871
        clr.w -(sp)
        move.l 28(a4),-(sp)
        move.w 22(a4),-(sp)
        move.l 4(a4),-(sp)
        move.w #$18,-(sp)
        dc.w $a9e7
        move.w (sp)+,32(a4)
        addq.w #1,34(a4)
        bra loop
key:
        ifd COLOR
        cmp.b #'h',13(a4)
        beq next_highlight
        cmp.b #'p',13(a4)
        beq next_palette
        endif
        ifd ICON_LISTS
        cmp.b #'n',13(a4)
        beq narrow_icons
        cmp.b #'w',13(a4)
        beq wide_icons
        cmp.b #'r',13(a4)
        beq reset_icons
        bra icon_keys_done
narrow_icons:
        moveq #48,d3
        bra size_icons
wide_icons:
        moveq #112,d3
size_icons:
        move.w d3,-(sp)
        move.w #64,-(sp)
        move.l 4(a4),-(sp)
        move.w #$14,-(sp)
        dc.w $a9e7
        add.w d3,d3
        move.w d3,-(sp)
        move.w #192,-(sp)
        move.l 4(a4),-(sp)
        move.w #$60,-(sp)
        dc.w $a9e7
        bsr draw
        bra loop
reset_icons:
        moveq #0,d3
clear_icon:
        clr.w -(sp)
        bsr icon_cell
        move.l d0,-(sp)
        move.l 4(a4),-(sp)
        move.w #$5c,-(sp)
        dc.w $a9e7
        addq.w #1,d3
        cmp.w #6,d3
        blt clear_icon
        bra loop
icon_keys_done:
        endif
        cmp.b #'r',13(a4)
        beq reset_selection
        cmp.b #'c',13(a4)
        beq condensed
        cmp.b #'n',13(a4)
        beq narrow
        cmp.b #'w',13(a4)
        beq wide
        cmp.b #'d',13(a4)
        beq deactivate
        cmp.b #'a',13(a4)
        beq activate
        cmp.b #'s',13(a4)
        beq single
        cmp.b #'m',13(a4)
        bne loop
        moveq #0,d0
        bra selection_mode
single:
        moveq #-128,d0
selection_mode:
        move.l 4(a4),a0
        move.l (a0),a0
        move.b d0,36(a0)
        bra loop
        ifd COLOR
next_highlight:
        addq.w #1,50(a4)
        cmp.w #6,50(a4)
        blt apply_colors
        clr.w 50(a4)
        bra apply_colors
next_palette:
        addq.w #1,52(a4)
        cmp.w #3,52(a4)
        blt apply_colors
        clr.w 52(a4)
apply_colors:
        move.l (a4),-(sp)
        dc.w $a873
        move.w 50(a4),d0
        mulu.w #6,d0
        lea highlight_colors(pc),a0
        adda.w d0,a0
        move.l a0,-(sp)
        dc.w $aa22
        move.w 52(a4),d0
        mulu.w #12,d0
        lea port_colors(pc),a0
        adda.w d0,a0
        pea 6(a0)
        move.l a0,-(sp)
        dc.w $aa14
        dc.w $aa15
        bsr draw
        addq.l #1,54(a4)
        bra loop
        endif
narrow:
        moveq #100,d3
        bra size_list
condensed:
        moveq #90,d3
        bra size_list
wide:
        move.w #250,d3
size_list:
        move.w d3,-(sp)
        move.w #16,-(sp)
        move.l 4(a4),-(sp)
        move.w #$14,-(sp)
        dc.w $a9e7
        move.w d3,-(sp)
        move.w #144,-(sp)
        move.l 4(a4),-(sp)
        move.w #$60,-(sp)
        dc.w $a9e7
        bsr draw
        bra loop
reset_selection:
        moveq #0,d3
clear_cell:
        clr.w -(sp)
        clr.w -(sp)
        move.w d3,-(sp)
        move.l 4(a4),-(sp)
        move.w #$5c,-(sp)
        dc.w $a9e7
        addq.w #1,d3
        cmp.w #12,d3
        blt clear_cell
        move.w #$100,-(sp)
        move.l #$00010000,-(sp)
        move.l 4(a4),-(sp)
        move.w #$5c,-(sp)
        dc.w $a9e7
        bra loop
deactivate:
        moveq #0,d0
        bra set_active
activate:
        move.w #$100,d0
        bra set_active
activate_event:
        move.w 22(a4),d0
        and.w #1,d0
        lsl.w #8,d0
set_active:
        move.w d0,-(sp)
        move.l 4(a4),-(sp)
        clr.w -(sp)
        dc.w $a9e7
        bra loop
update:
        move.l 10(a4),-(sp)
        dc.w $a922
        bsr draw
        move.l 10(a4),-(sp)
        dc.w $a923
        bra loop
draw:
        move.l (a4),-(sp)
        dc.w $a873
        move.l (a4),a0
        pea 16(a0)
        dc.w $a8a3
        move.l 4(a4),a0
        move.l (a0),a0
        move.l (a0),36(a4)
        move.l 4(a0),40(a4)
        subq.w #1,36(a4)
        subq.w #1,38(a4)
        addq.w #1,40(a4)
        add.w #17,42(a4)
        pea 36(a4)
        dc.w $a8a1
        move.l (a4),a0
        move.l 24(a0),-(sp)
        move.l 4(a4),-(sp)
        move.w #$64,-(sp)
        dc.w $a9e7
        rts
        ifd SICN_LISTS
fill_sicn:
        move.l 4(a4),a0
        move.l (a0),a0
        move.l #$000d0000,12(a0)
        lea sicn_colors(pc),a1
        move.l a1,68(a0)
        lea sicn_labels(pc),a3
        moveq #0,d3
fill_sicn_row:
        lea 58(a4),a0
        moveq #13,d0
clear_sicn_row:
        clr.l (a0)+
        dbra d0,clear_sicn_row
        move.w d3,d0
        add.w #256,d0
        move.w d0,74(a4)
        move.b #1,76(a4)
        move.w #12,86(a4)
        btst #0,d3
        beq sicn_left
        clr.b 76(a4)
        move.b #$ff,77(a4)
sicn_left:
        cmp.w #2,d3
        bne sicn_color_3
        move.w #2,78(a4)
sicn_color_3:
        cmp.w #3,d3
        bne sicn_disabled
        move.w #3,78(a4)
sicn_disabled:
        cmp.w #4,d3
        bne sicn_style
        move.b #1,84(a4)
sicn_style:
        cmp.w #5,d3
        bne sicn_label
        move.b #1,85(a4)
sicn_label:
        moveq #0,d4
        move.b (a3),d4
        move.l a3,a0
        lea 88(a4),a1
        move.w d4,d0
copy_sicn_label:
        move.b (a0)+,(a1)+
        dbra d0,copy_sicn_label
        lea 1(a3,d4.w),a3
        pea 58(a4)
        move.w d4,-(sp)
        add.w #31,(sp)
        clr.w -(sp)
        move.w d3,-(sp)
        move.l 4(a4),-(sp)
        move.w #$58,-(sp)
        dc.w $a9e7
        addq.w #1,d3
        cmp.w #8,d3
        blt fill_sicn_row
        rts
sicn_colors:
        dc.w $0000,$0000,$0000
        dc.w $cccc,$cccc,$ffff
        dc.w $ffff,$2222,$2222
sicn_labels:
        dc.b 12,'Applications',9,'Utilities',7,'Network',8,'Controls'
        dc.b 8,'Disabled',6,'Styled',13,'System Folder',11,'Color icons'
        even
        endif
        ifd ICON_LISTS
fill_icons:
        lea icon_labels(pc),a3
        moveq #0,d3
fill_icon:
        clr.l -(sp)
        move.l #'ICN#',-(sp)
        move.w d3,d0
        add.w #256,d0
        move.w d0,-(sp)
        dc.w $a9a0
        move.l (sp)+,58(a4)
        cmp.w #2,d3
        blt icon_loaded
        clr.w -(sp)
        pea 58(a4)
        move.w d3,d0
        add.w #256,d0
        move.w d0,-(sp)
        move.l #$ffffffff,-(sp)
        move.w #$0501,d0
        dc.w $abc9
        addq.l #2,sp
icon_loaded:
        clr.l 62(a4)
        move.w #12,66(a4)
        moveq #0,d4
        move.b (a3),d4
        move.l a3,a0
        lea 68(a4),a1
        move.w d4,d0
copy_label:
        move.b (a0)+,(a1)+
        dbra d0,copy_label
        lea 1(a3,d4.w),a3
        add.w #11,d4
        cmp.w #4,d3
        bne store_icon
        moveq #4,d4
store_icon:
        pea 58(a4)
        move.w d4,-(sp)
        bsr icon_cell
        move.l d0,-(sp)
        move.l 4(a4),-(sp)
        move.w #$58,-(sp)
        dc.w $a9e7
        addq.w #1,d3
        cmp.w #6,d3
        blt fill_icon
        rts
icon_cell:
        moveq #0,d0
        move.w d3,d0
        lsr.w #1,d0
        swap d0
        move.w d3,d0
        and.w #1,d0
        rts
icon_labels:
        dc.b 13,'System Folder',12,'Applications',34,'A very long icon application label'
        dc.b 9,'Utilities',0,0
        even
        endif
        ifd COLOR
highlight_colors:
        dc.w $0000,$0000,$0000
        dc.w $cccc,$cccc,$ffff
        dc.w $cccc,$ffff,$cccc
        dc.w $ffff,$cccc,$cccc
        dc.w $ffff,$ffff,$ffff
        dc.w $8888,$8888,$8888
port_colors:
        dc.w $0000,$0000,$0000,$ffff,$ffff,$ffff
        dc.w $1111,$2222,$4444,$ffff,$eeee,$cccc
        dc.w $cccc,$1111,$2222,$1111,$2222,$4444
        endif
view_rect:
        ifd ICON_LISTS
        dc.w 20,20,212,244
data_rect:
        dc.w 0,0,3,2
        else
        ifd SICN_LISTS
        dc.w 20,20,180,240
data_rect:
        dc.w 0,0,8,1
        else
        dc.w 20,20,164,270
data_rect:
        dc.w 0,0,12,1
        endif
        endif
labels:
        dc.b 13,'System Folder',12,'Applications',9,'Documents',9,'Utilities'
        dc.b 5,'Fonts',11,'Preferences',14,'Control Panels',10,'Extensions'
        dc.b 16,'Desk Accessories',9,'Scrapbook',9,'TeachText',5,'Trash'
        even
        ifd ICON_LISTS
        dc.b 'S7ICONS!'
        else
        ifd SICN_LISTS
        dc.b 'S7SICN!!'
        else
        dc.b 'S7LISTS!'
        endif
        endif
state:
        ds.b 58
        ifd ICON_LISTS
        ds.b 266
        endif
        ifd SICN_LISTS
        ds.b 256
        endif
